param([switch]$KeepDatabase)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$OutputEncoding=[System.Text.UTF8Encoding]::new($false)
$taskDatabase='nexo_inventario_test_'+[Guid]::NewGuid().ToString('N')
$oldTestConnection=$env:NEXO_TEST_MYSQL
function Invoke-TestSql([string]$Sql) {
    $Sql | docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot --default-character-set=utf8mb4 --batch'
    if($LASTEXITCODE -ne 0){throw 'Falló preparación/verificación SQL de inventario operativo.'}
}
try {
    Invoke-TestSql "CREATE DATABASE $taskDatabase CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"
    $batch="USE $taskDatabase;`n"
    foreach($file in Get-ChildItem "$PSScriptRoot/migrations/*.sql" | Sort-Object Name){$batch+=[System.IO.File]::ReadAllText($file.FullName)+"`n"}
    $batch+=[System.IO.File]::ReadAllText("$PSScriptRoot/seeds/001_demo.sql")+"`n"
    $batch+=[System.IO.File]::ReadAllText("$PSScriptRoot/seeds/002_catalogo_demo.sql")+"`n"
    $batch+="GRANT SELECT,INSERT,UPDATE ON $taskDatabase.* TO 'nexo_app'@'%';`n"
    $batch+="GRANT DELETE ON $taskDatabase.producto_proveedores TO 'nexo_app'@'%';`n"
    $batch+="GRANT DELETE ON $taskDatabase.usuario_roles TO 'nexo_app'@'%';`n"
    $batch+="GRANT DELETE ON $taskDatabase.rol_permisos TO 'nexo_app'@'%';`n"
    Invoke-TestSql $batch
    # Capturar sin imprimir secretos; ConnectionStringBuilder escapa caracteres especiales.
    $taskPassword=(docker compose exec -T mysql sh -c 'printf %s "$MYSQL_PASSWORD"') -join ''
    if($LASTEXITCODE -ne 0){throw 'No se pudo obtener credencial de prueba.'}
    $taskPort=(docker compose port mysql 3306).Trim().Split(':')[-1]
    $taskConnection=[System.Data.Common.DbConnectionStringBuilder]::new()
    $taskConnection['Server']='127.0.0.1';$taskConnection['Port']=$taskPort
    $taskConnection['Database']=$taskDatabase;$taskConnection['User ID']='nexo_app'
    $taskConnection['Password']=$taskPassword;$taskConnection['SslMode']='Preferred'
    $env:NEXO_TEST_MYSQL=$taskConnection.ConnectionString
    dotnet test backend/Nexo.sln -c Release --logger 'trx;LogFileName=inventario-operativo.trx'
    $testExit=$LASTEXITCODE
    # Invariantes reales después de ventas y anulaciones concurrentes.
    Invoke-TestSql @"
USE $taskDatabase;
SELECT 'Diferencias stock global/libro',COUNT(*) FROM
 (SELECT e.producto_id FROM existencias e LEFT JOIN movimientos_inventario m ON m.producto_id=e.producto_id GROUP BY e.producto_id,e.cantidad HAVING e.cantidad<>COALESCE(SUM(m.delta),0)) x;
SELECT 'Diferencias stock ubicaciones/libro',COUNT(*) FROM
 (SELECT e.producto_id,e.ubicacion_id FROM existencias_ubicacion e LEFT JOIN movimientos_inventario m ON m.producto_id=e.producto_id AND m.ubicacion_id=e.ubicacion_id GROUP BY e.producto_id,e.ubicacion_id,e.cantidad HAVING e.cantidad<>COALESCE(SUM(m.delta),0)) x;
SELECT 'Movimientos con saldos inconsistentes',COUNT(*) FROM movimientos_inventario WHERE cantidad_anterior IS NOT NULL AND cantidad_posterior<>cantidad_anterior+delta;
SELECT 'Conteos aplicados sin operación',COUNT(*) FROM conteos_inventario WHERE estado='APLICADO' AND operacion_id IS NULL;
SELECT 'Ventas con pagos inconsistentes',COUNT(*) FROM
 (SELECT v.id FROM ventas v LEFT JOIN pagos p ON p.venta_id=v.id GROUP BY v.id,v.total HAVING v.total<>COALESCE(SUM(p.importe_aplicado),0)) x;
"@
    if($testExit -ne 0){throw 'Hay pruebas fallidas; revisar los resultados TRX.'}
} finally {
    $env:NEXO_TEST_MYSQL=$oldTestConnection
    $taskPassword=$null;$taskConnection=$null
    if(-not $KeepDatabase){Invoke-TestSql "DROP DATABASE IF EXISTS $taskDatabase;"}
    else {Write-Host "Base de ensayo conservada: $taskDatabase"}
}
