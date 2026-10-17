# Manual catálogo e inventario

Relacionado: [[Fase 6]], [[Verificación Fase 6]], [[Sesiones en terminales compartidas]].

1. Iniciar sesión con Administrador. Reiniciar la API del IDE después de recompilar para cargar los endpoints nuevos.
2. En Categorías crear o editar nombres y estado. En Subcategorías seleccionar categoría. Desactivar conserva referencias históricas; las relaciones nuevas requieren maestros activos.
3. En Ubicaciones crear área o pasillo con código único. Para un estante, seleccionar ubicación superior. No se admiten ciclos. Una ubicación con saldo o hijos activos no puede desactivarse.
4. En Proveedores registrar nombre, identificación y teléfono. El acceso depende de proveedores.leer y proveedores.administrar.
5. En Productos buscar por nombre, SKU o barras; filtrar por categoría, subcategoría, ubicación y estado. Nuevo producto exige SKU único, categoría, precio, costo, unidad y mínimo. Seleccionar proveedores permite varias asociaciones. Editar carga los valores y la versión; si aparece conflicto, recargar y revisar antes de guardar.
6. Para desactivar un producto o maestro, editar, retirar Activo y guardar. No se elimina su historial. El filtro de productos Inactivos permite consultarlo y reactivarlo.
7. Inventario abre en la pestaña Existencias: filtrar por nombre/SKU, ubicación y estado. Usar Registrar movimiento para abrir el panel lateral; su búsqueda admite nombre, SKU o barras y hasta 25 coincidencias. Entrada añade saldo; salida retira; ajuste aplica una diferencia positiva o negativa. Todos requieren ubicación y motivo. Cerrar el panel conserva el borrador y la clave mientras no se recargue la página.
8. Traslado exige origen y destino diferentes: por ejemplo, retirar 4 de B-01 e ingresar 4 a R-01. El total de la tienda no cambia. Consultar existencias para comprobar ambas ubicaciones.
9. Ante error de red, reintentar con exactamente los mismos datos. No iniciar otra operación hasta comprobar si se registró la anterior; una clave nueva representa otra operación. La clave de reintento vive en memoria del formulario, por lo que recargar pierde esa clave.
10. Para hot dogs, nachos preparados y café caliente, retirar Controlar inventario. Si existe saldo, retirarlo primero mediante movimiento justificado. No se crean recetas ni se descuentan insumos.
11. Abrir la pestaña Alertas (su contador está siempre visible). Consultar alertas: Agotado significa cero; Stock bajo significa total <= mínimo. Los preparados sin inventario no generan alertas. El mínimo es global, no por estante.
12. Abrir Movimientos y desplegar Filtros de movimientos para consultar por fechas UTC, tipo y producto. Se muestran responsable, motivo, ubicación y cantidad con signo. Un traslado aparece como dos filas con el mismo número de operación.

Los datos DEMO-F6 son exclusivamente ficticios. Este módulo no registra todavía ventas ni facturas: corresponde a la siguiente fase.

En Existencias o Alertas, Ver detalle abre otro panel con el total, el mínimo y la distribución por ubicación. Escape o Cerrar vuelve a la tabla. Registrar movimiento de este producto inicia un borrador nuevo con el producto y, cuando corresponda, la ubicación seleccionados. El panel no se cierra mientras se guarda.
