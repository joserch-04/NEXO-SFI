# Fase 2 — Base de datos y Docker

Fecha: 15 de octubre de 2026. Relación: [[Modelo preliminar]], [[Diagrama entidad-relación]], [[Verificación Fase 2]], [[Docker local]], [[Decisiones pendientes]].

## Objetivo y tecnologías

Convertir el modelo de Fase 1 en SQL versionado y reproducible, con datos sintéticos y MySQL 8.4 LTS en Docker Compose. Se utilizan InnoDB, utf8mb4, PowerShell 7 y SQL nativo, sin Entity Framework. La solicitud de Fase 2 autoriza esta implementación; no resuelve las políticas comerciales pendientes.

## Archivos y flujo

`compose.yaml` crea MySQL, el volumen persistente y un healthcheck con una consulta autenticada. `.env.example` enumera variables sin secretos. `.env` local contiene claves aleatorias y está ignorado. El puerto está vinculado exclusivamente a loopback; no permite acceso MySQL directo desde terminales LAN.

`database/Manage-Database.ps1` tiene tres acciones. `Migrate` calcula SHA256 de los SQL, comprueba `schema_migrations`, mantiene un bloqueo nombrado de MySQL durante la aplicación y registra cada versión al terminar. `Invoke-Sql` envía SQL por entrada estándar al cliente dentro del contenedor y comprueba su código de salida. La contraseña se obtiene dentro del contenedor y no aparece en argumentos ni salidas. El DDL de MySQL hace commits implícitos: un error parcial requiere inspección y recuperación, no un rollback prometido. No hay migración destructiva automática ni downgrade.

`Seed` carga una transacción completa con IDs fijos; no es una actualización de datos de producción. Si se repite, falla sin sobrescribir datos. `Verify` comprueba tablas, stock contra libro, totales de detalle/pagos y rechazos por CHECK, FK, unicidad de turno e idempotencia. Estas pruebas requieren las semillas sin modificaciones.

`001_esquema.sql` crea las 26 tablas de dominio y el script crea la tabla técnica número 27. Todas las FK conservan historia mediante RESTRICT/NO ACTION, sin cascadas. Los índices de FK simples se crean automáticamente en InnoDB; los compuestos y de consultas principales son explícitos. El SQL es la referencia exacta de campos, tipos, PK, FK, UNIQUE y CHECK; [[Diagrama entidad-relación]] refleja esas relaciones.

## Tablas y decisiones

|Grupo|Tablas y finalidad|
|---|---|
|Seguridad|usuarios, roles, permisos, usuario_roles, rol_permisos, sesiones_refresh: identidad, asignaciones y hashes de tokens revocables.|
|Catálogo|categorias, productos, proveedores, producto_proveedores: catálogo y abastecedores.|
|Inventario|existencias, operaciones_inventario, movimientos_inventario: saldo operativo y libro con fuentes trazables.|
|Caja|cajas, turnos_caja, movimientos_caja: apertura/cierre, efectivo y movimientos manuales.|
|Venta|ventas, detalle_ventas, pagos, facturas: importes, instantáneas del producto, efectivo y comprobante académico.|
|Reversiones|anulaciones, devoluciones, detalle_devoluciones: autorización e historial de devoluciones.|
|Control|operaciones_idempotentes, auditoria, respaldos: deduplicación, trazabilidad y metadatos de copias.|

Importes DECIMAL(18,2), precios/costos DECIMAL(18,4), cantidades DECIMAL(18,3), tiempos DATETIME(6) en UTC. La BD permite representar fracciones, sin aprobar todavía su venta. No se fija tasa legal ni fórmula tributaria. La única forma de pago habilitada por CHECK es efectivo y el comprobante es académico.

Una columna generada nullable con UNIQUE impide dos turnos abiertos por caja y admite varios cerrados. No se impone un turno único por cajero: D07 sigue pendiente. Una FK compuesta exige que venta, caja y usuario correspondan al mismo turno. Las devoluciones usan FK compuestas para que sus detalles pertenezcan a la venta original.

El usuario de aplicación recibe SELECT/INSERT/UPDATE por tabla, sin DDL ni DELETE; auditoría y libro de inventario solo SELECT/INSERT; migraciones solo SELECT. Esto conserva historia desde la aplicación. El administrador local root se reserva para estas herramientas; privilegios de tablas puente y controles de estados se revisarán al implementar sus casos de uso. Las cuentas demo están deshabilitadas con hash vacío, sin credenciales utilizables: Fase 4 deberá crear cuentas con PasswordHasher antes de habilitarlas.

## Límites entre BD y aplicación

CHECK protege una fila, no sumas entre filas. La aplicación futura deberá bloquear turno/saldos y confirmar venta, detalle, pagos, stock, comprobante y auditoría en una sola transacción. También deberá validar turno abierto, usuarios activos, descuentos/impuestos, suma de pagos, pertenencia del producto devuelto, devoluciones acumuladas, signos de ajustes, cierre irreversible y prohibición de editar historia. Las pruebas de esta fase no demuestran esos casos de uso ni concurrencia de ventas.

No hay endpoints, clases C# ni métodos de negocio implementados. No se ha decidido un contador fiscal ni secuencia legal; la unicidad serie/número protege comprobantes, pero la asignación bajo bloqueo se implementará tras aprobar su formato. Los respaldos aquí son únicamente estructura de metadatos; la interfaz de recuperación pertenece a Fase 9.

## Ejecutar y defender

Comandos en el README de la raíz. Antes de aplicar migraciones sobre datos reales, realizar respaldo. Nunca modificar el checksum registrado para ocultar un cambio. Ante fallo parcial, detener escrituras, inspeccionar tablas y recuperar desde copia o corregir mediante un procedimiento revisado.

- ¿Por qué un saldo y un libro? El saldo permite consultar y bloquear rápido; el libro explica cada variación y permite conciliar.
- ¿Por qué DECIMAL? Conserva precisión decimal en importes y cantidades; float introduce aproximaciones binarias.
- ¿Por qué SQL versionado? Permite revisar la evolución y reproducir la misma estructura con Dapper, sin EF.
- ¿El volumen es un backup? No; una copia independiente y restaurable sigue siendo necesaria.
- ¿Qué garantiza idempotencia? La clave única evita duplicarla en BD; devolver la respuesta original exige la transacción y validación del backend futuro.
- ¿Qué está verificado? Consultar [[Verificación Fase 2]]; no confundir esquema probado con una venta funcional por API.

## Fuentes e incidencias

Se consultaron las [notas oficiales 8.4](https://dev.mysql.com/doc/relnotes/mysql/8.4/en/) y la [guía oficial de Docker](https://dev.mysql.com/doc/refman/8.4/en/linux-installation-docker.html). La etiqueta `mysql:8.4.12` no se pudo resolver en Docker Hub; se eligió `mysql:8.4.11` disponible. Esto no afirma que sea la última revisión: antes de producción revisar imagen y actualizaciones de seguridad. Las notas de Oracle distinguen sus imágenes de las etiquetas publicadas en Docker Hub.

El primer acceso desde el entorno restringido no encontró el motor; el acceso autorizado fuera de ese entorno detectó Docker Desktop y el motor Linux. Resultados restantes en [[Verificación Fase 2]].
