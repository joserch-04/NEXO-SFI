# Requisitos

Estado: propuesto. Contexto: [[README]]; diseño: [[Arquitectura]], [[API REST]], [[Plan de pruebas]]. Cada criterio deberá demostrarse con pruebas en su fase correspondiente.

## Actores y permisos

Administrador: administración, reportes, respaldos y autorización de operaciones críticas. Cajero: consulta de catálogo, ventas y movimientos permitidos de su propio turno. Consulta: solo lectura de los recursos expresamente autorizados. Los permisos son políticas verificadas por el backend; el rol no sustituye la comprobación de propiedad del turno. La asignación inicial exacta se aprueba antes de implementar seguridad.

## Requisitos funcionales y aceptación

| ID   | Requisito                         | Criterio de aceptación previsto                                                                                                                                                 |
| ---- | --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF01 | Iniciar, renovar y cerrar sesión  | Credenciales válidas permiten entrar; cerrar sesión revoca la renovación; credenciales inválidas no revelan si existe el usuario.                                               |
| RF02 | Administrar usuarios              | Crear, editar y desactivar con nombre único; usuario desactivado no inicia ni renueva sesión.                                                                                   |
| RF03 | Administrar roles y permisos      | Asignación autorizada y auditada; un permiso ausente impide la operación por API.                                                                                               |
| RF04 | Administrar productos             | SKU único, código de barras opcional único, precio y mínimo válidos; productos históricos se desactivan.                                                                        |
| RF05 | Administrar categorías            | Crear y editar; referencias históricas impiden eliminación física.                                                                                                              |
| RF06 | Administrar proveedores           | Alta, edición y desactivación; datos mínimos por confirmar.                                                                                                                     |
| RF07 | Registrar entradas                | Documento con uno o más productos incrementa saldo y registra proveedor cuando corresponda, costo y responsable.                                                                |
| RF08 | Registrar salidas y ajustes       | Motivo obligatorio, permiso específico, saldo sin negativos y movimiento auditable.                                                                                             |
| RF09 | Mostrar stock bajo                | Lista productos activos con saldo menor o igual al mínimo.                                                                                                                      |
| RF10 | Administrar cajas                 | Identificador único; desactivación rechazada mientras exista turno abierto.                                                                                                     |
| RF11 | Abrir y cerrar turno              | Una caja admite un solo turno abierto; apertura registra fondo y cierre registra contado y diferencia.                                                                          |
| RF12 | Confirmar venta con detalle       | Usuario, caja y turno válidos; detalle, pago, comprobante e inventario se guardan juntos o nada se guarda.                                                                      |
| RF13 | Calcular importes en servidor     | API rechaza descuentos no permitidos y recalcula subtotal, impuesto y total sin aceptar totales del cliente.                                                                    |
| RF14 | Generar comprobante imprimible    | Venta confirmada produce número único y representación imprimible reproducible desde datos históricos.                                                                          |
| RF15 | Registrar pagos                   | Inicialmente efectivo; importe recibido y cambio permiten conciliar el importe aplicado a la venta.                                                                             |
| RF16 | Anular venta                      | Administrador autorizado registra motivo y reversión una sola vez; conserva el original.                                                                                        |
| RF17 | Devolver productos                | Cantidad acumulada devuelta no supera la vendida; reembolso y reingreso dependen de reglas aprobadas.                                                                           |
| RF18 | Registrar movimientos de efectivo | Ingreso o egreso con motivo afecta el esperado del turno y queda auditado.                                                                                                      |
| RF19 | Arqueo y conciliación             | Cierre muestra fondo + cobros efectivos − reembolsos + ingresos − egresos frente al efectivo contado.                                                                           |
| RF20 | Consultar historial               | Filtros por fecha, cajero, caja y estado; consulta de cabecera y detalle sin modificar originales.                                                                              |
| RF21 | Auditar acciones críticas         | Actor, instante, acción, recurso, resultado y correlación consultables sin contraseñas ni tokens.                                                                               |
| RF22 | Dashboard                         | Tres gráficos de ventas diarias, productos más vendidos y ventas por método con datos reales y período filtrable.                                                               |
| RF23 | Ocho reportes                     | Inventario actual; movimientos por período; ventas por fechas; ventas maestro-detalle; ventas por cajero/turno; cierres/diferencias; ventas por método; productos más vendidos. |
| RF24 | Exportar Excel                    | Exportación autorizada conserva filtros y totales del reporte; neutraliza contenido interpretable como fórmulas.                                                                |
| RF25 | Respaldar y restaurar             | Administrador confirma acción; archivo validado, resultado auditado y recuperación demostrada en entorno de prueba.                                                             |

## Requisitos no funcionales

|ID|Requisito y verificación prevista|
|---|---|
|RNF01|Operación LAN sin Internet: completar venta con salida a Internet desconectada y servidor accesible. Si cae el servidor no se pueden confirmar ventas.|
|RNF02|Consistencia: transacciones InnoDB, bloqueo concurrente e idempotencia; verificar carreras y rollback.|
|RNF03|Seguridad: HTTPS confiable, contraseñas con PasswordHasher, SQL parametrizado, JWT validado, refresh revocable y autorización por recurso.|
|RNF04|Privacidad: secretos fuera de Git, logs sin credenciales, mínimo acceso a archivos y MySQL.|
|RNF05|Rendimiento propuesto: percentil 95 de consultas frecuentes ≤2 s y confirmación ≤3 s en LAN con 3 cajas concurrentes y 10 000 productos; medir y aprobar hardware/dataset antes de aceptar este objetivo. Exportaciones y respaldos quedan fuera.|
|RNF06|Recuperación propuesta: respaldo diario, RPO 24 h y RTO 2 h; dependen de tamaño, equipo, copia externa y ensayo. No son resultados medidos.|
|RNF07|Mantenibilidad: capas, scripts SQL versionados, OpenAPI y pruebas críticas; sin EF Core.|
|RNF08|Usabilidad: formularios con validación, navegación por teclado, foco visible y errores claros; verificar manualmente el flujo de caja.|
|RNF09|Trazabilidad: conservar documentos históricos, importes y movimientos; auditar modificaciones administrativas. Retención pendiente.|
|RNF10|Reproducibilidad: configuración documentada, volumen persistente, versiones fijadas al implementar y restauración ensayada.|
|RNF11|Fechas e importes: guardar tiempos UTC, mostrar America/Managua; usar decimal y política de redondeo aprobada. Filtros locales se convierten a intervalos UTC semiabiertos.|

## Reglas transversales propuestas

No vender stock negativo. No permitir editar ventas confirmadas. Una venta requiere turno abierto propio y caja activa. La desactivación no elimina historia. Cambios de precio afectan ventas futuras; el detalle conserva nombre, SKU, precio e impuesto aplicados. Se propone impedir más de un turno abierto por cajero. Deben aprobarse descuentos, impuestos, cantidades fraccionarias, anulaciones y devoluciones antes de implementar cálculos.
