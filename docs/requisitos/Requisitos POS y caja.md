# Requisitos POS y caja

Complementa [[Requisitos]], [[Fase 7]], [[Modelo POS y caja]] y [[Verificación Fase 7]]. Fecha: 18 de octubre de 2026.

|ID|Requisito funcional y aceptación|
|---|---|
|RF7-01|Seleccionar imagen desde el equipo al crear o editar producto; previsualizar sin guardar todavía.|
|RF7-02|Reemplazar o retirar la fotografía con permiso de catálogo y versión vigente.|
|RF7-03|Mostrar foto o recurso predeterminado en catálogo y tarjetas POS.|
|RF7-04|Administrar código, nombre, activación y fuera de servicio de cajas.|
|RF7-05|Consultar cajas disponibles y ocupadas y su turno abierto.|
|RF7-06|Abrir un turno propio con fondo no negativo; impedir dos aperturas en la misma caja.|
|RF7-07|Buscar por nombre, SKU o barras y usar lector de teclado con Enter en el campo de búsqueda.|
|RF7-08|Filtrar categorías/subcategorías, añadir al carrito, variar cantidad o quitar líneas.|
|RF7-09|Calcular subtotal, descuentos autorizados, impuesto configurado y total en servidor.|
|RF7-10|Confirmar venta con detalle histórico, cajero, caja, turno, fecha, cliente opcional y número único.|
|RF7-11|Descontar inventario por ubicación una sola vez; rechazar insuficiencia y omitir stock para preparados.|
|RF7-12|Registrar efectivo, tarjeta y transferencia, solos o combinados, sin datos financieros sensibles.|
|RF7-13|Validar cobertura completa, importe aplicado y cambio exclusivamente en efectivo.|
|RF7-14|Generar comprobante interno con emisor histórico, productos, totales, pagos y cambio.|
|RF7-15|Visualizar, imprimir y reimprimir en formato térmico de 80 mm y A4.|
|RF7-16|Consultar ventas propias o de todos con permiso de historial, filtrando por turno.|
|RF7-17|Registrar entradas/salidas de efectivo con importe, motivo y responsable, solo en turno propio abierto.|
|RF7-18|Anular con permiso específico, motivo y devolución explícita de efectivo; conservar venta y pagos.|
|RF7-19|Restituir las ubicaciones originales una sola vez y dejar reembolso bancario como pendiente externo.|
|RF7-20|Arqueo: fondo + efectivo neto + ingresos − egresos − devoluciones; mostrar otros medios separados.|
|RF7-21|Cerrar con contado, diferencia, observaciones e historial imprimible; impedir otro cierre y nuevas ventas.|
|RF7-22|Auditar las operaciones sensibles con identidad y fecha del servidor, vinculadas al recurso detallado.|

## Reglas e integridad

- Un usuario opera su turno propio. `cajas.historial` amplía lectura de ventas; para consultar turnos y arqueos ajenos también se exige el rol Administrador. No habilita escritura sobre turnos ajenos.
- No se vende ni se retira efectivo de un turno cerrado. Una caja fuera de servicio no admite apertura.
- Cantidad positiva con tres decimales; precio con cuatro; descuento, impuesto, total y pago con dos.
- No hay stock negativo. Los preparados no verifican ni descuentan stock y no consumen ingredientes.
- Cabecera, detalles, pagos, comprobante, stock, idempotencia y auditoría son atómicos.
- Clave UUID única por actor/tipo y hash de solicitud; repetición idéntica devuelve el resultado previo.
- El estado ANULADA conserva el historial. UNIQUE por venta y bloqueo común impiden duplicar restitución.
- Una anulación con efectivo es también autorización de devolver ese efectivo desde el turno indicado. Tarjeta/transferencia no se reembolsan automáticamente.
- Las consultas exponen ventas anuladas diferenciadas; los cobros históricos no se borran de los arqueos.
- Un turno cerrado conserva su arqueo; las devoluciones posteriores afectan el turno que entrega el dinero.
- La tasa inicial es cero hasta aprobación tributaria. La configuración actual admite una tasa general sobre precio neto; no impuestos múltiples ni exenciones por producto.

## Requisitos no funcionales

- HTTPS, JWT y permisos vigentes en backend; la ocultación de botones es solo experiencia de usuario.
- SQL parametrizado y MySQL transaccional; sin Entity Framework.
- Fotografías normalizadas, límites de bytes/píxeles, inspección binaria y nombres independientes del original.
- Persistencia de fotos fuera de la capa efímera del contenedor y referencias relativas en BD.
- Sin secretos ni datos completos de tarjetas en código, tablas, auditoría o comprobantes.
- Operación interna en LAN; no se requiere Internet, sí conexión activa a API/MySQL.
- Diseño adaptable y operable con teclado. Las pruebas con dispositivos e impresoras físicos se distinguen de las simuladas.
- Documentación española Markdown y enlaces Obsidian; resultados únicamente de pruebas ejecutadas.

## RF7-23 — Referencias de pagos

Exigir referencia por cada pago TARJETA o TRANSFERENCIA nuevo, incluidos pagos combinados; conservarla en consulta, comprobante, reimpresión y anulación. Preservar ceros iniciales, validar tipo/longitud y respetar permisos y transacción. Simular terminal NFC independiente sin aprobación bancaria real. Los pagos históricos sin referencia se conservan. Véase [[Referencias de pagos]].
