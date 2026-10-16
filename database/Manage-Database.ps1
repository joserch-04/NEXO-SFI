param(
    [ValidateSet('Migrate','Seed','Verify')][string]$Action = 'Verify'
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$PSNativeCommandArgumentPassing = 'Standard'
function Invoke-Sql([string]$Sql, [switch]$ExpectFailure, [switch]$AsApp) {
    $client = if ($AsApp) { 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysql -unexo_app --default-character-set=utf8mb4 --batch --skip-column-names nexo' } else { 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot --default-character-set=utf8mb4 --batch --skip-column-names nexo' }
    $result = $Sql | docker compose exec -T mysql sh -c $client 2>&1
    $code = $LASTEXITCODE
    if ($ExpectFailure) {
        if ($code -eq 0) { throw 'Se aceptó una operación que debía rechazarse.' }
        return ($result -join "`n")
    }
    if ($code -ne 0) { throw ($result -join "`n") }
    return ($result -join "`n")
}
if ($Action -eq 'Migrate') {
    # El bloqueo se conserva en UNA conexión durante todas las migraciones.
    $sql = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
 version VARCHAR(100) PRIMARY KEY, checksum CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 aplicado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;
"@
    Invoke-Sql $sql | Out-Null
    $batch = @"
DELIMITER //
CREATE PROCEDURE nexo_migration_lock()
BEGIN
 IF GET_LOCK('nexo_migrations',10) <> 1 THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Otra migracion esta en curso';
 END IF;
END//
DELIMITER ;
CALL nexo_migration_lock();
DROP PROCEDURE nexo_migration_lock;
"@
    foreach ($file in Get-ChildItem "$PSScriptRoot/migrations/*.sql" | Sort-Object Name) {
        $version = $file.BaseName
        if ($version -notmatch '^\d{3}_[a-z_]+$') { throw "Nombre inválido: $version" }
        $checksum = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $stored = Invoke-Sql "SELECT checksum FROM schema_migrations WHERE version='$version';"
        if ($stored) {
            if ($stored -ne $checksum) { throw "Checksum modificado: $version" }
            Write-Host "Ya aplicada: $version"
            continue
        }
        # Comprobar otra vez bajo bloqueo antes de DDL evita doble aplicación concurrente.
        $batch += @"

DELIMITER //
CREATE PROCEDURE nexo_migrate_$version()
BEGIN
 IF EXISTS(SELECT 1 FROM schema_migrations WHERE version='$version') THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migracion aplicada concurrentemente; reintente';
 END IF;
END//
DELIMITER ;
CALL nexo_migrate_$version();
DROP PROCEDURE nexo_migrate_$version;
"@
        $batch += "`n" + (Get-Content $file.FullName -Raw)
        $batch += "`nINSERT INTO schema_migrations(version,checksum) VALUES('$version','$checksum');`n"
    }
    $batch += "`nDO RELEASE_LOCK('nexo_migrations');"
    Invoke-Sql $batch | Write-Host
    # La imagen crea inicialmente nexo_app con ALL; reducir privilegios tras instalar.
    $grants = "REVOKE ALL PRIVILEGES, GRANT OPTION FROM 'nexo_app'@'%';`n"
    $tables = (Invoke-Sql "SELECT table_name FROM information_schema.tables WHERE table_schema='nexo' AND table_type='BASE TABLE';") -split "`n"
    foreach ($table in $tables) {
        $table = $table.Trim()
        if ($table -notmatch '^[a-z_]+$') { throw 'Tabla no válida' }
        $rights = if ($table -eq 'schema_migrations') { 'SELECT' } elseif ($table -in @('auditoria','movimientos_inventario')) { 'SELECT, INSERT' } else { 'SELECT, INSERT, UPDATE' }
        $grants += "GRANT $rights ON nexo.$table TO 'nexo_app'@'%';`n"
    }
    Invoke-Sql $grants | Out-Null
    Write-Host 'Migraciones aplicadas. DDL MySQL no es transaccional: no reintentar un fallo parcial sin inspección.'
} elseif ($Action -eq 'Seed') {
    Invoke-Sql (Get-Content "$PSScriptRoot/seeds/001_demo.sql" -Raw) | Write-Host
} else {
    Invoke-Sql (Get-Content "$PSScriptRoot/tests/integridad.sql" -Raw) | Write-Host
    $cases = @(
        @{ Sql="UPDATE existencias SET cantidad=-1 WHERE producto_id=1;"; Error='3819' },
        @{ Sql="UPDATE usuarios SET activo=TRUE WHERE id=1;"; Error='3819' },
        @{ Sql="INSERT INTO productos(categoria_id,sku,nombre,precio) VALUES(999999,'INVALIDO','FK',1);"; Error='1452' },
        @{ Sql="INSERT INTO turnos_caja(caja_id,usuario_id,apertura_en,fondo_inicial,estado) VALUES(2,2,UTC_TIMESTAMP(6),0,'ABIERTO');"; Error='1062' },
        @{ Sql="INSERT INTO operaciones_idempotentes(usuario_id,tipo,clave,hash_solicitud,recurso_id,estado_http,creado_en) SELECT usuario_id,tipo,clave,hash_solicitud,recurso_id,estado_http,creado_en FROM operaciones_idempotentes WHERE id=1;"; Error='1062' }
    )
    foreach ($case in $cases) {
        # ROLLBACK también si se acepta inesperadamente: pruebas sin cambios persistidos.
        $result = Invoke-Sql ("START TRANSACTION;`n" + $case.Sql + "`nROLLBACK;") -ExpectFailure
        if ($result -notmatch "ERROR $($case.Error)") { throw "Fallo inesperado: $result" }
        Write-Host "Rechazo correcto: MySQL $($case.Error)"
    }
    Invoke-Sql 'SELECT COUNT(*) FROM productos;' -AsApp | Out-Null
    foreach ($denied in @('DELETE FROM productos WHERE id=999999;', 'UPDATE auditoria SET resumen=resumen WHERE id=999999;', 'CREATE TABLE no_autorizada(id INT);')) {
        $result = Invoke-Sql $denied -ExpectFailure -AsApp
        if ($result -notmatch 'ERROR 1142') { throw "Fallo de permisos inesperado: $result" }
        Write-Host 'Usuario de aplicación: operación administrativa rechazada (1142).'
    }
    Write-Host 'Integridad, permisos y datos demo verificados.'
}
