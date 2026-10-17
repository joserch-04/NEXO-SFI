# API REST

Contrato preliminar vinculado a [[Requisitos]] y [[Modelo preliminar]]. Base `/api/v1`; JSON UTF-8, HTTPS. Fase 3 implementa únicamente GET /api/v1/health/live y GET /api/v1/health/ready, más OpenAPI en desarrollo; véase [[Fase 3]]. Fase 4 implementa las rutas de sesión, usuarios, roles y permisos; véase [[Fase 4]] para sus contratos efectivos y restricciones. Las demás rutas siguen propuestas.

## Convenciones

JWT Bearer salvo login/refresh; logout requiere cookie y protección CSRF. Políticas por permiso y comprobación adicional de propiedad de turno. Listados paginados (`page=1`, `pageSize=25`, máximo propuesto 100), orden estable y filtros permitidos. Importes decimales en JSON, tratados con decimal en servidor; fechas ISO 8601 UTC, filtros locales convertidos explícitamente. GET sin efectos de negocio.

201 + Location al crear; 200 al consultar/actualizar o recuperar resultado idempotente (puede conservar 201 original); 204 al desactivar o cerrar sesión sin cuerpo; 400 entrada inválida; 401 sesión inválida; 403 permiso insuficiente; 404 inexistente; 409 conflicto de estado, stock o clave reutilizada; 429 límite de intentos. Errores Problem Details con `code`, `traceId` y errores por campo, sin SQL ni secretos. Nunca devolver stack traces al cliente.

## Rutas propuestas

Las rutas agrupadas con `{id}` se expandirán en OpenAPI durante implementación. DELETE significa desactivación solo para maestros; no elimina documentos históricos.

|Método y ruta|Operación / permiso propuesto|
|---|---|
|POST /auth/login|usuario y contraseña; público con límites de intentos y CSRF/Origin.|
|POST /auth/refresh|Rotar cookie refresh; validación CSRF/Origin.|
|POST /auth/logout|Revocar familia y borrar cookie; CSRF/Origin.|
|GET /auth/me|Identidad y permisos efectivos, sesión autenticada.|
|GET, POST /usuarios; GET, PUT, DELETE /usuarios/{id}|usuarios.leer / usuarios.administrar; password nunca se devuelve.|
|PUT /usuarios/{id}/roles|roles.asignar; impedir autoescalamiento y pérdida del último administrador.|
|POST /usuarios/{id}/cambio-contrasena|Propietario con contraseña actual o administrador autorizado; revocar sesiones.|
|GET, POST /roles; GET, PUT, DELETE /roles/{id}|roles.leer / roles.administrar.|
|GET /permisos; PUT /roles/{id}/permisos|roles.leer / roles.administrar; permisos técnicos no se crean libremente.|
|GET, POST /productos; GET, PUT, DELETE /productos/{id}|catalogo.leer / catalogo.administrar.|
|GET, POST /categorias; GET, PUT, DELETE /categorias/{id}|catalogo.leer / catalogo.administrar.|
|GET, POST /proveedores; GET, PUT, DELETE /proveedores/{id}|proveedores.leer / proveedores.administrar.|
|GET /inventario/existencias; GET /inventario/stock-bajo|inventario.leer; filtros de categoría/producto.|
|GET /inventario/movimientos|inventario.leer; fechas, producto, tipo.|
|POST /inventario/entradas|inventario.entradas; proveedor opcional, referencia y líneas producto/cantidad/costo.|
|POST /inventario/salidas; POST /inventario/ajustes|inventario.ajustar; motivo y líneas con delta; idempotencia obligatoria.|
|GET, POST /cajas; GET, PUT, DELETE /cajas/{id}|cajas.leer / cajas.administrar.|
|GET /turnos; GET /turnos/{id}|turnos.leer; cajero limitado a los propios.|
|POST /turnos|turnos.abrir; cajaId y fondoInicial, usuario inferido del token.|
|POST /turnos/{id}/cierre|turnos.cerrar; efectivoContado y observación; cierre propio, excepción administrativa auditada.|
|GET, POST /turnos/{id}/movimientos|caja.leer / caja.movimientos; solo turno propio abierto para registrar.|
|POST /ventas|ventas.crear; confirmación atómica e idempotente.|
|GET /ventas; GET /ventas/{id}|ventas.leer; filtros fecha/cajero/caja/estado según alcance del rol.|
|GET /ventas/{id}/comprobante|comprobantes.leer; datos y representación imprimible del documento académico.|
|POST /ventas/{id}/anulaciones|ventas.anular; motivo y turno de reembolso; autorización administrativa.|
|POST /ventas/{id}/devoluciones|ventas.devolver; detalleVentaId, cantidad, destinoStock, motivo y turno; autorización administrativa.|
|GET /reportes/{reporte}|reportes.leer; identificador de catálogo cerrado de ocho reportes.|
|GET /reportes/{reporte}/excel|reportes.exportar; mismos filtros y restricciones que consulta.|
|GET /dashboard|dashboard.leer; desde/hasta y series reales.|
|GET /auditoria|auditoria.leer; filtros actor/fecha/acción.|
|GET, POST /respaldos; GET /respaldos/{id}|respaldos.administrar; creación como trabajo 202 con consulta de estado.|
|GET /respaldos/{id}/archivo|respaldos.administrar; descarga controlada por ID.|
|POST /restauraciones|restauraciones.ejecutar; respaldoId, confirmación explícita y reautenticación; 202 y seguimiento del trabajo.|
|GET /restauraciones/{id}|restauraciones.ejecutar; estado del trabajo, guardado fuera de la BD restaurada.|
|GET /health/live; GET /health/ready|Sondeos mínimos sin datos internos; disponibilidad de BD solo en readiness.|

Permisos sugeridos: administrador recibe el catálogo aprobado; cajero consulta productos, abre/cierra su turno, registra ventas y movimientos autorizados; consulta accede únicamente a lecturas asignadas. Exportar, ver auditoría y administrar respaldos requieren permisos separados. La lista administrativa efectiva y sus restricciones se documentan en [[Fase 4]].

## Venta: ejemplo conceptual

POST /ventas, cabecera `Idempotency-Key: <UUID generado por terminal>`.

Cuerpo: `turnoId`, `lineas` con `productoId`, `cantidad` y descuento solicitado si está habilitado; `pago` con método EFECTIVO e importe recibido. No aceptar usuarioId ni precios/totales como autoridad del cliente. Servidor obtiene caja y usuario del turno validado. Respuesta: id, número de comprobante, líneas con precios aplicados, subtotal, descuento, impuesto, total, importe aplicado, recibido y cambio.

Ejemplo sin impuestos ni descuentos, solo ilustrativo: 2 unidades a C$25 producen total C$50; recibido C$100 y cambio C$50. No constituye una regla tributaria aprobada.

La clave es obligatoria en venta, reversión, inventario y movimientos/apertura/cierre de caja; asociada a usuario y tipo de operación. Reintentos idénticos recuperan resultado; contenido diferente devuelve 409. Las actualizaciones de maestros deben detectar edición concurrente mediante versión o ETag (412 si If-Match quedó obsoleto).

## Reportes

Identificadores permitidos: inventario-actual, movimientos-inventario, ventas-periodo, ventas-detalle, ventas-cajero-turno, cierres-caja, ventas-metodo-pago, productos-mas-vendidos. Validar rango máximo propuesto de 366 días, paginación y máximo de filas de Excel por aprobar. Movimientos y ventas requieren período; inventario actual representa el saldo presente, no inventario histórico. Anulaciones/devoluciones se muestran diferenciadas y se descuentan de métricas netas con fecha de reversión; definición pendiente de aprobación.

## Restauración y operación

No aceptar comandos, rutas del sistema ni nombres SQL del cliente. Solo respaldos generados o importados mediante validación futura explícita; la versión inicial propone restaurar respaldos propios. La restauración usa un ejecutor administrativo separado del proceso que atiende ventas, activa mantenimiento, impide nuevas escrituras, cierra conexiones y exige respaldo previo. Estado/log operacional fuera de la BD a restaurar; finalizar revocando sesiones. El mecanismo concreto pertenece a Fase 9 y requiere ensayo antes de habilitar la interfaz.

## Contrato efectivo Fase 6

Las rutas de catálogo e inventario ya están implementadas; véase [[Fase 6]] para contratos, filtros y permisos definitivos. Incluye `/subcategorias`, `/ubicaciones` y `/inventario/traslados`. La clave idempotente se envía en el campo JSON `key` UUID para movimientos. Los maestros usan `version` explícita y responden 409 si quedó obsoleta, en lugar del ETag/412 inicialmente propuesto. Los ajustes aplican diferencias con signo. La interfaz y API de ventas siguen pendientes de Fase 7.

## Configuración de identidad del negocio

GET `/api/v1/configuracion/negocio`: sesión autenticada; PUT en la misma ruta: Administrador y configuracion.administrar. HTTPS y no-store. JSON name, tagline, version; PUT devuelve perfil normalizado y versión incrementada. 400 entrada inválida; 409 versión antigua. Véase [[Identidad y configuración del negocio]].
