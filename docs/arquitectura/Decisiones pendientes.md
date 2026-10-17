# Decisiones pendientes

Estado actualizado: la solicitud expresa de Fase 2 autoriza implementar la estructura de base de datos y Docker. Las reglas comerciales y fiscales D01–D08 no se consideran resueltas por esa autorización. Ver [[Fase 2]] y [[Verificación Fase 2]]. Enlaces: [[README]], [[Requisitos]], [[Arquitectura]], [[Modelo preliminar]], [[API REST]], [[Docker local]], [[Plan de pruebas]].

## Decisiones propuestas y justificación

|ID|Propuesta|Justificación / pendiente|
|---|---|---|
|D01|Una tienda, una ubicación de stock y moneda NIO.|Reduce alcance académico; confirmar con usuario/tutor.|
|D02|Comprobante académico no fiscal inicialmente.|No se ha definido alcance legal ni formato con tutor. Confirmar emisor, serie, numeración y campos; si se exige factura fiscal, investigar normativa antes de diseñarla.|
|D03|Solo efectivo inicialmente, sin pagos mixtos ni bancos.|Cumple mínimo y simplifica conciliación; esquema permite evolución posterior aprobada.|
|D04|Cantidades enteras inicialmente, aunque BD permite tres decimales.|Confirmar si se vende por peso o fracción antes de validar cantidades.|
|D05|Precios finales o base imponible, tasas, exenciones y redondeo por definir.|Bloquea implementación de cálculo. Propuesta a revisar: redondear importes de línea a dos decimales y sumar; definir orden de descuento e impuesto. No asumir tasa legal.|
|D06|Descuentos deshabilitados hasta aprobar reglas.|Definir por línea/global, porcentaje/importe, límites y autoridad. No inventar reglas comerciales.|
|D07|Un turno abierto por caja y por cajero; cierre irreversible.|Confirmar relevo de cajero, permisos de cierre ajeno, fondo, retiros y diferencias aceptables.|
|D08|Anulación íntegra una vez y solo sin devoluciones previas; devoluciones parciales autorizadas.|Confirmar plazo, reingreso vendible/dañado, documento de reversión y devolución después de cierre. Propuesta: reembolso en turno abierto actual, no alterar turno cerrado original.|
|D09|Monolito modular, Dapper, SQL versionado e inventario sin negativos.|Consistencia sencilla y stack obligatorio. No agregar compras contables, lotes/vencimientos ni clientes sin aprobación.|
|D10|HTTPS mismo origen, access en memoria y refresh cookie rotatoria.|Confirmar nombre LAN, certificado y equipos; 10 min/8 h son plazos propuestos. Definir retención y matriz final de permisos antes de F4.|
|D11|Backup diario, 7 diarios + 4 semanales, RPO 24 h/RTO 2 h.|Confirmar capacidad, copia externa/cifrado, responsable y ensayo; no objetivos garantizados.|
|D12|3 cajas concurrentes, 10 000 productos, p95 lectura 2 s/venta 3 s.|Confirmar volumen y hardware para prueba significativa.|
|D13|Métricas netas descuentan reversiones en fecha de reversión.|Confirmar criterio de ventas brutas/netas, impuestos y valoración; evitar ambigüedad en reportes.|
|D14|Conservar `AGENTS.md.md` de momento.|Autorizar normalización a AGENTS.md para descubrimiento convencional; no se renombró.|

## Aprobación solicitada

Aprobar o corregir arquitectura, alcance y modelo preliminar, y resolver prioritariamente D01–D08. D10–D13 pueden completarse antes de sus fases, pero quedan visibles como riesgos del diseño. Tras aprobar esta Fase 1, autorizar expresamente Fase 2; no iniciar backend ni frontend junto con ella. Cualquier cambio fiscal o de varias ubicaciones requiere revisar modelo y API antes del DDL.

## Defensa técnica: preguntas y respuestas propuestas

- ¿Por qué funciona sin Internet? El servidor, base y recursos están en la tienda; la LAN debe seguir disponible. No hay venta desconectada del servidor.
- ¿Por qué separar capas? HTTP, casos de uso, reglas y acceso a datos tienen responsabilidades distintas, de modo que las reglas no dependen de React ni del SQL del controlador.
- ¿Cómo se evita vender la última unidad dos veces? Se bloquea el saldo dentro de la transacción y se verifica disponibilidad antes de actualizar; debe demostrarse con dos solicitudes concurrentes.
- ¿Qué pasa si se pierde la respuesta de una venta? La clave idempotente permite recuperar la venta ya confirmada sin crear otra.
- ¿Por qué guardar datos del producto en detalle? El cambio posterior de nombre o precio no debe reescribir el comprobante histórico.
- ¿Por qué el volumen no es respaldo? Conserva datos del contenedor, pero no protege por sí mismo frente a corrupción, borrado o pérdida del host.
- ¿La factura es legal? Todavía no se ha definido ni verificado ese alcance; se propone un comprobante académico hasta aprobación con tutor.
- ¿Qué está implementado? Nada funcional. Esta entrega es un diseño revisable; las pruebas descritas son futuras.

## Riesgos y límites

Sin política fiscal no se pueden cerrar fórmulas ni formato de factura. Sin reglas de devolución no se puede cerrar conciliación. La restauración desde interfaz exige mantenimiento y un ejecutor separado para no depender de la base que se reemplaza. La disponibilidad depende de energía/LAN y de respaldos recuperables; no se propone alta disponibilidad. No se consultaron fuentes externas ni se validaron versiones instaladas porque la etapa documenta el stack prescrito, no instrucciones de instalación ni afirmaciones legales.


## Actualización aprobada en Fase 6

La solicitud expresa de catálogo e inventario autoriza múltiples ubicaciones y estantes, y cantidades de hasta tres decimales. D01 queda actualizado a una tienda con varias ubicaciones y moneda NIO; D04 utiliza la precisión existente del esquema. El diseño efectivo se describe en [[Fase 6]]. D02–D08, en lo relativo a fiscalidad, ventas y caja, no se consideran resueltos por esta fase. El texto histórico anterior conserva el contexto original.
