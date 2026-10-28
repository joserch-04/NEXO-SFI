# Manual catálogo e inventario

Actualizado: 27 de octubre de 2026. Relacionado: [[Fase 6]], [[Inventario operativo]], [[Verificación inventario operativo]] y [[Manual POS y caja]].

1. Iniciar sesión con una cuenta autorizada. Tras actualizar, recompilar y reiniciar la API, recargar el cliente e iniciar sesión para renovar permisos.
2. Administrar Categorías y Subcategorías desde Catálogo. Desactivar conserva historial; las relaciones nuevas requieren maestros activos.
3. En Ubicaciones crear áreas, pasillos y estantes con código único. Un estante puede tener ubicación superior; no se admiten ciclos ni desactivar ubicaciones con saldo o hijos activos.
4. En Proveedores registrar nombre, identificación y contacto. Sus permisos son independientes de inventario.
5. En Productos registrar SKU, barras opcionales, nombre, categoría/subcategoría, precio, costo, unidad, mínimo y proveedores. Buscar por nombre, SKU o barras y filtrar. La edición utiliza versión para evitar sobrescribir cambios ajenos.
6. Para desactivar, editar y retirar Activo. No se elimina historial. El filtro Inactivos permite consultar y reactivar.
7. En **Inventario → Existencias**, consultar totales y distribución por ubicación. Alertas permanece dentro de esta pantalla: Agotado significa cero; Stock bajo significa total igual o inferior al mínimo global. Los productos sin control de inventario quedan excluidos.
8. En **Operaciones → Entradas de mercancía**, seleccionar proveedor, referencia de documento y motivo; agregar productos, ubicación, cantidad y costo unitario de recepción. Confirmar todas las líneas juntas. El costo registrado no modifica automáticamente el costo del catálogo.
9. En **Operaciones → Salidas y mermas**, seleccionar daño, vencimiento, pérdida, consumo interno u otro motivo. Indicar productos, cantidades y origen. No se permite saldo negativo. Vencimiento es un motivo manual, sin gestión de lotes.
10. En **Operaciones → Traslados**, seleccionar origen y destino diferentes. Por ejemplo, trasladar cuatro gaseosas de B-01 a R-01 conserva el total y actualiza ambas ubicaciones.
11. Ante respuesta de red incierta, utilizar Reintentar registro con los mismos datos. Navegar al historial y regresar conserva el intento dentro de Inventario. No recargar ni crear otra operación antes de verificar el resultado. Salir de Inventario, recargar o cerrar sesión pierde borradores en memoria.
12. En **Conteos y ajustes**, crear un conteo para una ubicación y seleccionar productos. Registrar cantidad física: cero significa agotado; una casilla vacía significa pendiente. Guardar y enviar a revisión cuando todas estén completas.
13. Una cuenta con inventario.aprobar puede aprobar el conteo pendiente. Solo entonces se aplican diferencias mediante una operación auditable. Si hubo movimientos desde su creación, se rechaza: cancelar y crear otro conteo actualizado. Los aplicados no se editan; borradores y pendientes pueden cancelarse con permiso de captura.
14. En **Historial de movimientos**, consultar fechas UTC, tipo, producto y ubicación. Se muestran cantidad anterior, variación, cantidad posterior, responsable, motivo, proveedor, referencia y costo cuando corresponda. Un traslado tiene dos filas. Los registros antiguos sin saldos históricos muestran «—». También incluye ventas y anulaciones del POS.
15. Para hot dogs, nachos preparados y café caliente, retirar Controlar inventario. Si existe saldo, retirarlo antes con justificación. Se venden desde POS sin exigir existencias; no se implementan recetas ni descuentos de ingredientes.

Los documentos admiten hasta cien líneas. Requieren productos activos con control de inventario y ubicaciones activas. Consulta, entradas, ajustes, captura y aprobación tienen permisos verificados también en servidor.

Los datos DEMO-F6 y las capturas de ensayo son ficticios. Rutas y limitaciones en [[Inventario operativo]].

## Uso de la interfaz simplificada

En entradas, salidas y traslados, agregar los productos y pulsar **Continuar**. Revisar las líneas y completar los datos de la operación; **Editar productos** permite corregir antes de registrar. La ayuda adicional se consulta en el desplegable inferior.

En Conteos se abre primero la lista. **Nuevo conteo** muestra únicamente la creación; **Ver conteo** abre su detalle. Guardar cantidades y Enviar a aprobación se presentan según el avance de la captura. Volver a la lista cierra esa tarea; se solicita confirmación si hay cantidades sin guardar.

## Catálogo simplificado y selección de productos

La presentación actual se explica en [[Diseño del catálogo y búsqueda de productos]]. En Productos, buscar filtra automáticamente. Más filtros despliega categoría, subcategoría y ubicación. Nuevo producto, Editar y Ver detalle abren una ventana; Fotografía y Proveedores se despliegan dentro de la edición. Los proveedores se marcan mediante casillas.

En Categorías, Subcategorías, Proveedores y Ubicaciones, la vista inicial es la lista con búsqueda; Crear o Editar abre su formulario. Las cuentas de consulta no muestran acciones de escritura.

En inventario, escribir en Buscar producto despliega las coincidencias debajo del mismo campo. Elegir con clic o flechas/Enter; no existe un selector adicional. La X permite cambiar de producto. El campo Costo de compra por unidad (C$) es el costo de la recepción y no el precio de venta.
