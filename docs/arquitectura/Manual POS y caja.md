# Manual POS y caja

Véanse [[Fase 7]], [[Requisitos POS y caja]], [[Modelo POS y caja]] y [[Verificación Fase 7]].

## Preparar productos e imágenes

En Productos, crear o editar un registro autorizado. Elegir una foto PNG, JPG o WebP de hasta 2 MB; revisar la vista previa y guardar. Se genera una miniatura PNG de hasta 256×256. «Eliminar fotografía» retira la referencia al guardar. Si falla la fotografía después de guardar el producto, el formulario pasa a editar ese mismo registro para reintentar.

Los preparados como hot dog, nachos o café caliente deben mantener «Controlar inventario» desactivado. No requieren existencias para vender. Los demás productos deben tener saldo cargado mediante los movimientos de Fase 6.

## Abrir caja

1. Ingresar con la cuenta personal. En la sección «Punto de venta» del menú abrir «Caja y turnos», o usar «Gestionar caja» desde el POS.
2. En «Mi turno», desplegar «Abrir mi turno». Seleccionar una caja activa, disponible y operativa; ingresar fondo inicial contado físicamente.
3. Confirmar «Confirmar apertura». Se muestra el turno propio. Si otra terminal abrió primero, recargar y seleccionar otra caja.

Solo Administración puede crear/editar cajas. No se permite desactivar una caja abierta. Un cajero puede tener un solo turno abierto.

## Atender una venta

1. En Punto de venta, buscar por nombre, SKU o barras; o filtrar categoría/subcategoría.
2. Un lector configurado como teclado debe escribir sobre «Buscar o escanear» y terminar con Enter. La coincidencia exacta de barras o SKU agrega el producto y devuelve el foco al buscador.
3. Pulsar tarjetas para agregar. Usar +, − o cantidad decimal; «Quitar» elimina una línea.
4. Desplegar «Agregar cliente» cuando corresponda. «Aplicar descuento» se abre en cada línea solo para usuarios autorizados; el descuento es un importe, no un porcentaje.
5. El catálogo ocupa la página sin contenedor de altura fija. Agregar productos mantiene las imágenes visibles y solo actualiza el contador y total. La barra inferior compacta ofrece «Ver venta» para abrir cantidades, descuentos y cliente en una ventana; «Seguir agregando» la cierra. Pulsar «Cobrar» para abrir la ventana centrada de pago. Elegir Efectivo, Tarjeta NFC, Transferencia o Pago combinado. Solo se muestran los campos de la opción elegida. Al cambiar de método se limpian importes y referencias anteriores para no arrastrar pagos ocultos; se precarga el total en pagos individuales. En efectivo puede usarse «Importe exacto» o indicar el dinero recibido. No introducir datos de tarjeta ni cuentas bancarias.
6. Revisar total, importe faltante o cambio y la referencia de los pagos no efectivos. «Volver a la venta» o Escape conserva cliente, carrito, descuentos e importes mientras no haya un cobro pendiente. «Confirmar cobro» envía líneas y pagos; el servidor recalcula precios e impuestos actuales y confirma stock disponible.
7. Consultar el comprobante definitivo. Imprimir en térmica de 80 mm o A4 según el equipo. El diálogo del navegador permite elegir impresora.

Ejemplo: total C$75, tarjeta C$25 y efectivo recibido C$100. Se aplican C$50 de efectivo y C$25 de tarjeta; se devuelven C$50. La caja gana C$50, no C$100. Un exceso cobrado solo con tarjeta se rechaza porque no corresponde entregar cambio en efectivo.

Si se pierde la conexión durante el cobro, usar «Reintentar venta». El sistema conserva cuerpo y UUID de la solicitud pendiente. No recargar ni iniciar otro cobro hasta verificar el resultado. Si ya se recargó, consultar historial del turno antes de cobrar otra vez. Navegar del POS a caja o historial dentro de la sección conserva el carrito; salir de la sesión o recargar lo descarta.

## Consultar y reimprimir

«Ventas y comprobantes» muestra fecha, caja/turno, cajero, estado y total; admite filtro por número de turno y paginación. El cajero ve sus ventas; `cajas.historial` permite consultar las de todos. «Ver / reimprimir» usa el snapshot del comprobante, no los datos actuales del catálogo. Se presentan como «Comprobante de venta», sin leyendas fiscales, tanto en pantalla como en impresión térmica y A4.

## Movimientos y anulaciones

Las entradas/salidas de efectivo requieren `cajas.movimientos`, importe positivo y motivo. Se registran sobre el turno propio abierto. No usar un egreso manual adicional para una devolución ya registrada por anulación, porque se restaría dos veces.

Para anular, una persona con `ventas.anular` debe tener su propio turno abierto. Abrir el comprobante, desplegar anulación, indicar motivo y confirmar. La pantalla declara que devuelve el efectivo aplicado desde ese turno. Revisar físicamente la entrega. El stock se restituye a sus ubicaciones originales. La venta se conserva con estado ANULADA.

Una anulación de tarjeta o transferencia no revierte el banco: registra un pendiente que debe gestionarse y conciliarse externamente. La fase no tiene una pantalla para certificar su liquidación. El permiso de anular incluye responsabilizar la entrega de efectivo indicada; no debe asignarse sin autorización del negocio.

## Arqueo y cierre

1. Abrir Caja y turnos → Mi turno → «Arqueo y cierre». Para un cierre anterior: Historial → «Ver arqueo».
2. Revisar fondo inicial, efectivo aplicado, ingresos, egresos y devoluciones.
3. Revisar tarjeta/transferencia por separado y pendientes de reembolso externo.
4. Desplegar «Contar efectivo y cerrar turno». Contar el efectivo físico; la pantalla muestra contado−esperado como sobrante o faltante.
5. Registrar observaciones y confirmar cierre definitivo.
6. Consultar el historial y usar «Imprimir resumen» para conservar el cierre.

Ejemplo: fondo C$100 + efectivo aplicado C$250 + entradas C$20 − salidas C$30 − devoluciones C$50 = C$290 esperado. Contado C$285 → diferencia −C$5. Tarjeta y transferencia no se suman al efectivo.

El cierre impide nuevas ventas/movimientos y no puede repetirse como otro cierre. Un reintento con la misma clave recupera el resultado previo. Los cierres anteriores no se reabren para devolver dinero: una devolución posterior se imputa a un turno abierto del responsable autorizado.

## Referencias de pagos externos

Al ingresar un importe de tarjeta aparece «Referencia de la operación con tarjeta»; copiar el identificador del voucher aprobado de la terminal NFC simulada. Para transferencia, registrar la referencia después de verificar la recepción externa. Cada método no efectivo utilizado necesita su propia referencia. El efectivo no lleva ese dato. No ingresar número de tarjeta ni CVV. El comprobante y su reimpresión conservan las referencias. Véase [[Referencias de pagos]].


La distribución vigente de pantallas, menú y rutas está documentada en [[Navegación por secciones]]. POS, caja e historial tienen pantallas independientes. La opción de tarjeta se presenta como «Tarjeta · terminal NFC».
### Quién puede consultar un arqueo

El cajero ve sus propios turnos y arqueos, abiertos o cerrados, cuando dispone de cajas.leer y cajas.arquear. Solo el Administrador con cajas.historial puede consultar los turnos de otros cajeros; necesita cajas.arquear para abrir su arqueo. Un permiso de historial asignado a un cajero no amplía ese acceso. Las operaciones de cierre siguen limitadas al turno propio abierto.

## Pantalla de caja por tareas

«Mi turno» muestra la caja abierta, fondo inicial y acciones principales. Los formularios de apertura y movimiento permanecen plegados hasta elegir su acción. «Historial» permite consultar aperturas y arqueos; «Cajas registradoras» muestra disponibilidad y ofrece crear o editar solo con el permiso existente. El formulario de caja aparece al pulsar Nueva caja o Editar caja y puede ocultarse con Cancelar.

Mientras una apertura, movimiento o cierre está en proceso o sin respuesta confirmada, se bloquea el cambio interno de sección para conservar el formulario y su clave. Usar el reintento indicado; no recargar ni salir de la página ante un resultado incierto.
