# Verificación Fase 2

Fecha de ejecución: 15 de octubre de 2026, America/Managua. Relación: [[Fase 2]], [[Plan de pruebas]], [[Diagrama entidad-relación]].

## Entorno observado

- Docker Desktop 4.44.3; motor Linux 28.3.2; Compose v2.39.1-desktop.1.
- MySQL observado mediante `SELECT VERSION()`: **8.4.11**.
- Imagen descargada: `mysql:8.4.11`, fijada en Compose con digest `sha256:24989179f58856db763f418010c1169fecc944e3ec1ea7e921572d83eb070f70`.
- Contenedor `nexo-mysql-1`, volumen `nexo_mysql_data`, publicación `127.0.0.1:13306->3306/tcp`.
- No existe repositorio Git inicializado en la carpeta; no se hicieron commits. `.gitignore` queda preparado.

## Resultados ejecutados

|Comprobación|Resultado observado|
|---|---|
|`docker compose config --quiet`|Configuración válida sin mostrar secretos.|
|`docker compose up -d --wait`|Contenedor healthy; healthcheck realiza SELECT 1 autenticado.|
|`pwsh -File database/Manage-Database.ps1 Migrate`|001_esquema y 002_usuarios_seguros registradas.|
|Repetir Migrate después de 001|Reconoció checksum y omitió el DDL aplicado.|
|`pwsh -File database/Manage-Database.ps1 Seed`|Carga transaccional completa.|
|`pwsh -File database/Manage-Database.ps1 Verify`|27 tablas: 26 de dominio y una técnica; dataset completo; stock/libro y pagos/detalle/venta coinciden.|
|Stock negativo|UPDATE rechazado con ERROR 3819 (CHECK).|
|Activar usuario demo sin hash|UPDATE rechazado con ERROR 3819.|
|Categoría inexistente|INSERT rechazado con ERROR 1452 (FK).|
|Segundo turno abierto de caja 2|INSERT rechazado con ERROR 1062 (UNIQUE).|
|Clave idempotente repetida|INSERT rechazado con ERROR 1062.|
|Lectura con nexo_app|Consulta devolvió cuatro productos.|
|DELETE, UPDATE de auditoría y CREATE TABLE con nexo_app|Las tres operaciones fueron rechazadas con ERROR 1142.|
|`docker compose up -d --force-recreate --wait`|Contenedor recreado y saludable; Verify pasó otra vez conservando datos y migración 001.|
|Respaldo y restauración aislada|mysqldump consistente de nexo, restaurado en nexo_phase2_restore: 27 tablas, una venta, total 50.00 y suma de saldos 16.000; todas las aserciones de integridad.sql pasaron en la copia. Se eliminó únicamente la base temporal de prueba.|

Las pruebas negativas de Verify se ejecutan dentro de transacciones; si una operación se aceptara inesperadamente, el ROLLBACK evita persistirla. El script exige el código de error esperado y falla ante errores distintos. No demuestra solicitudes API ni prueba de carga.

## Dataset conocido

Tres roles, cuatro usuarios deshabilitados, tres permisos ilustrativos, dos categorías, un proveedor, cuatro productos y dos cajas. Un turno cerrado y uno abierto. Venta de dos aguas a 25.00: total 50.00, efectivo recibido 100.00, cambio 50.00. Saldo de agua 10, arroz 1, jugo 0 y producto inactivo 5. La carga inicial del agua fue 12 y su venta resta 2. Sin impuestos ni descuentos: ejemplo sintético, no decisión fiscal.

## Incidencias resueltas

1. El entorno restringido no accedió a configuración/motor Docker. La ejecución autorizada fuera del entorno encontró el motor Linux operativo.
2. `mysql:8.4.12` no existe en el registro consultado. Se descargó 8.4.11 y se fijó el digest real. Consultar [[Fase 2]] para las fuentes y el límite de esta elección.
3. Windows rechazó el enlace a 3306. `.env` local usa 13306; el ejemplo mantiene 3306 configurable. No se detuvieron otros servicios.

## Reproducir el ensayo de recuperación

Con una base de prueba y el contenedor saludable, se usaron dentro del contenedor estas operaciones administrativas. La contraseña se toma de la variable local del contenedor, sin imprimirla. Ejecutar desde `docker compose exec mysql sh`; no usar una base temporal que ya contenga datos necesarios.

```sh
export MYSQL_PWD="$MYSQL_ROOT_PASSWORD"
mysql -uroot -e 'CREATE DATABASE nexo_phase2_restore CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci'
dump=$(mktemp)
mysqldump -uroot --single-transaction --no-tablespaces --set-gtid-purged=OFF nexo > "$dump"
mysql -uroot nexo_phase2_restore < "$dump"
rm "$dump"
```

Desde PowerShell, verificar la copia con el archivo del proyecto:

```powershell
Get-Content database/tests/integridad.sql -Raw | docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot nexo_phase2_restore'
```

Después de validar, eliminar exclusivamente la base temporal creada para el ensayo. Esta prueba es de recuperación lógica local; no implementa todavía copias externas, retención, interfaz administrativa ni mide RPO/RTO. El archivo temporal del contenedor se eliminó; no se entregó como respaldo de producción.

## Pendiente y límites

Pruebas de concurrencia de ventas/cierre, API, Dapper, autorización de endpoints, cálculos aprobados, generación de comprobantes, carga de 10 000 productos y recuperación operativa pertenecen a fases posteriores. No se afirma bloqueo por red desde otra terminal: se verificó el enlace loopback mediante Docker. El motor sigue encendido y el volumen conserva los datos demo. El usuario administrador del host sigue teniendo capacidad de modificar datos mediante root.
