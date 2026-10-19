# Referencias de pagos

Ampliación de [[Fase 7]], [[Manual POS y caja]], [[Modelo POS y caja]] y [[Verificación Fase 7]]. Solicitud: simular una terminal NFC independiente y registrar la referencia de cada cobro con tarjeta y transferencia.

## Opinión y funcionamiento en producción

El enfoque es adecuado para el alcance local y académico de NEXO: una terminal independiente procesa la tarjeta y el cajero copia en la venta la referencia de la operación aprobada. NFC es la forma de presentar el medio de pago a la terminal; el método contable continúa siendo TARJETA. La aplicación no necesita leer la tarjeta ni almacenar PAN o CVV.

Existen dos esquemas distintos:

1. **Terminal independiente:** se introduce el importe en la terminal, se revisa su resultado y se registra el pago en caja. Después se concilian las operaciones de la terminal con ventas, anulaciones y liquidaciones. Adyen describe explícitamente terminales independientes, pagos NFC y conciliación manual en su [documentación oficial](https://docs.adyen.com/point-of-sale/standalone/).
2. **Terminal integrada:** el sistema envía el importe al proveedor y recibe el estado e identificador de transacción por API. Por ejemplo, Adyen utiliza identificadores del pago original para una devolución referenciada; véase [devolución referenciada](https://docs.adyen.com/point-of-sale/basic-tapi-integration/refund-payment/referenced/). Esa integración requiere especificar proveedor y protocolo antes de implementarla; esta fase no simula una API bancaria ni genera aprobaciones reales.

La referencia del cobro no debe confundirse con el depósito o liquidación posterior del banco. Por eso el campo se llama **Referencia de la operación**. Para una transferencia, la referencia sirve para relacionar el dinero recibido con la venta; escribir un código no verifica el ingreso. Como ejemplo de un proveedor real, [Stripe documenta datos de transferencias entrantes](https://docs.stripe.com/payments/bank-transfers), incluidos referencia y red, y su conciliación.

Recomendación operativa para producción: confirmar importe, moneda y resultado en la terminal o en la cuenta receptora antes de cerrar la venta. Una referencia escrita o una captura aportada por el cliente no reemplaza comprobar la recepción. Esta recomendación no implica que NEXO haya consultado un banco.

## Flujo implementado

1. Preparar el carrito y registrar el importe en Tarjeta · terminal NFC (simulada), Transferencia bancaria o ambos.
2. Al ingresar un importe positivo, aparece el campo de referencia de ese método.
3. Para tarjeta se copia la referencia del voucher aprobado de la terminal simulada; para transferencia se registra la referencia de la recepción verificada en el escenario de demostración.
4. El servidor exige referencia en cada pago no efectivo, aunque el navegador sea omitido. Efectivo no admite referencia bancaria.
5. Se guarda la referencia en el mismo pago y la misma transacción de venta.
6. Consulta de ventas → Ver/reimprimir muestra el comprobante con cada referencia. Las versiones térmica y A4 usan el mismo contenido.

NEXO no contacta terminales NFC, lectores, bancos ni pasarelas. El texto de la pantalla declara el registro manual de pagos externos simulados. No se añade un botón que fabrique una aprobación bancaria.

## Validación y modelo

Migración aditiva `009_referencias_pago.sql`: `pagos.referencia VARCHAR(100) NULL`. Conserva NULL en registros históricos y exige en BD que EFECTIVO tenga referencia NULL. La obligatoriedad para nuevos pagos TARJETA/TRANSFERENCIA se aplica en `PosService.Allocate`; no se inventan referencias para pagos anteriores.

- Referencia tratada como texto: conserva ceros iniciales y mayúsculas/minúsculas.
- Hasta 100 caracteres; letras ASCII, dígitos, espacios, guion, guion bajo, punto, barra y dos puntos. No se aceptan controles, saltos de línea ni HTML.
- Se recortan espacios exteriores antes de persistir.
- Se registra una referencia por método dentro de cada venta, respetando el límite existente de un pago por método.
- Los importes, cobertura del total, cambio y arqueo mantienen sus reglas. Ninguna referencia se suma al efectivo ni modifica los importes.
- No hay campos para tarjeta completa, CVV, cuentas ni credenciales. En la referencia solo debe ingresarse el identificador de operación; el servidor no puede certificar que un texto arbitrario provenga del banco.
- La referencia se conserva al anular y se utiliza como dato de consulta; el reembolso de tarjeta/transferencia continúa pendiente de gestión externa.

No se impone una unicidad global a las referencias copiadas: sin identificar banco, adquirente, comercio o terminal no se puede asumir que sus numeraciones compartan un espacio único. La idempotencia de venta continúa protegiendo reintentos de la misma solicitud. Para automatizar la detección de un mismo pago externo usado en ventas distintas habría que incorporar el identificador único y el ámbito real del proveedor; no se declara esa protección como implementada.

## Contrato HTTP

Se amplía POST `/api/v1/ventas`; no se crean endpoints nuevos ni se amplían permisos. `reference` es obligatoria cuando el método no sea EFECTIVO:

```json
{
  "key": "fdb4abb0-2348-44af-b561-2fdf4eafecdb",
  "shiftId": 7,
  "customer": null,
  "lines": [{"productId": 12, "quantity": 1, "discount": 0}],
  "payments": [
    {"method": "EFECTIVO", "received": 5},
    {"method": "TARJETA", "received": 10, "reference": "NFC-00001234"},
    {"method": "TRANSFERENCIA", "received": 10, "reference": "TRF-00005678"}
  ]
}
```

Ejemplo ilustrativo para una venta de C$25: sustituir IDs y precios por los reales. GET `/api/v1/ventas/{id}/comprobante` devuelve `payments[].reference`. Si una venta anterior no la tenía, muestra «Sin referencia (registro anterior)»; no se modifica ese pago histórico.

Errores 400: `payment_reference_required`, `invalid_payment_reference`, `cash_reference_not_allowed`. `idempotency_conflict` sigue siendo 409. Cambiar la referencia usando la misma clave de una venta ya confirmada produce conflicto, no modifica la operación original.

## Archivos y decisiones técnicas

|Archivo|Cambio|
|---|---|
|`Nexo.Domain/Pos.cs`|`PaymentInput.Reference` opcional para deserialización y compatibilidad; `SalePayment.Reference` para consulta.|
|`Nexo.Application/PosService.cs`|Valida referencia en pagos nuevos y devuelve el texto normalizado.|
|`Nexo.Infrastructure/MySqlPosStore.cs`|INSERT y lectura parametrizada en la transacción existente.|
|`frontend/nexo-web/src/pos.tsx`|Campos condicionales, bloqueo de cobro sin referencia, conservación del borrador y presentación imprimible.|
|`frontend/nexo-web/src/api.ts`|Errores específicos en español.|
|`database/migrations/009_referencias_pago.sql`|Columna y CHECK sin reescribir pagos anteriores.|
|`database/seeds/001_demo.sql`|INSERT de pago con columnas explícitas para seguir funcionando al ampliar la tabla.|

Los archivos de las capas son relativos a `backend/`. No se agregaron paquetes ni permisos.

La clave de idempotencia incluye el contenido de los pagos y, por tanto, las referencias enviadas. `PaymentInput.Reference` usa `JsonIgnore(WhenWritingNull)`: solicitudes antiguas sin ese dato conservan exactamente el JSON usado para calcular su hash. Si una venta antigua ya fue confirmada, puede recuperarse mediante el mismo UUID aunque no tuviera referencia. Una venta nueva no efectiva sin referencia se rechaza.

La auditoría de venta sigue confirmándose con la operación y permite llegar a sus pagos/referencias mediante el ID; no se crea una segunda escritura financiera ni se almacenan datos sensibles en mensajes de error.

## Pruebas y resultados

Los casos nuevos verifican obligatoriedad por método, rechazo de referencia vacía/control/HTML/exceso de longitud, normalización y ceros iniciales, mezcla de los tres métodos, conservación en comprobante/anulación, conflicto por cambio de referencia con el mismo UUID y recuperación de una venta histórica con su hash previo. Véanse resultados ejecutados en [[Verificación Fase 7]].

## Preguntas del jurado

**¿NEXO cobra realmente por NFC?** No. Simula el registro posterior a una terminal independiente. El procesamiento de tarjeta corresponde a la terminal/proveedor.

**¿Por qué no llamarlo número de depósito?** El identificador de operación permite localizar el cobro; no demuestra que el banco ya haya liquidado ese importe al negocio.

**¿Una referencia significa que la transferencia llegó?** No. El operador debe comprobar su recepción externamente; sin una integración bancaria NEXO no puede verificarla.

**¿Por qué no es un número entero?** Puede contener letras o ceros iniciales; almacenarlo como número destruiría información.

**¿Por qué se permiten referencias repetidas entre ventas?** El identificador manual no tiene un ámbito global definido. No se debe bloquear una operación de otro proveedor con numeración coincidente. La integración futura deberá identificar proveedor/terminal/transacción antes de añadir esa restricción.

**¿La anulación hace el reembolso?** Conserva la referencia para gestionarlo, pero no llama al banco. El pendiente no efectivo permanece trazable.
