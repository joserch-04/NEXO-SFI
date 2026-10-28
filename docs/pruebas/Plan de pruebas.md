# Plan de pruebas

Plan general para las fases futuras. Las pruebas SQL/Docker de Fase 2 ya se ejecutaron y están registradas en [[Verificación Fase 2]]. Fase 3 agrega pruebas unitarias, integración HTTP y sondeo MySQL: [[Verificación Fase 3]]. Enlaces: [[Requisitos]], [[Modelo preliminar]], [[Docker local]].

|Caso|Requisitos|Escenario y resultado esperado|Nivel / fase|
|---|---|---|---|
|P01|RF01–03, RNF03|Login, expiración, rotación, reutilización de refresh, logout y usuario desactivado; renovación rechazada cuando corresponda.|Integración, F4|
|P02|RF02–06|Unicidad SKU/usuario, validación, desactivación y preservación de historia.|Integración, F4/F6|
|P03|RF07–09|Entrada, ajuste y salida; saldo coincide con libro; salida excesiva rechazada.|Unidad/integración, F6|
|P04|RF10–11|Dos aperturas simultáneas de la misma caja: exactamente una tiene éxito.|MySQL real, F7|
|P05|RF12–15|Venta de varias líneas; verificar cálculo, pago, cambio y comprobante persistido.|Unidad/integración, F7|
|P06|RF12, RNF02|Forzar fallo entre pagos e inventario: ninguna parte queda confirmada.|Integración, F7|
|P07|RF12, RNF02|Dos cajas venden última unidad: una confirma y otra recibe conflicto; saldo cero.|Concurrencia MySQL real, F7|
|P08|RF12, RNF02|Repetir clave tras respuesta perdida: misma venta; clave con otro contenido: 409. Incluir peticiones simultáneas.|Integración, F7|
|P09|RF11–12|Cierre y venta simultáneos: venta queda incluida en cierre o se rechaza por turno cerrado.|Concurrencia, F7|
|P10|RF16–17|Anular dos veces o devolver más de lo vendido, incluso en paralelo: rechazar exceso; conservar trazabilidad.|Integración, F7|
|P11|RF18–19|Fondo 100 + cobros 250 − reembolsos 20 + ingresos 30 − egresos 10 = esperado 350; contado 345 da diferencia -5.|Unidad/integración, F7|
|P12|RF20–24|Ocho reportes y tres gráficos contra dataset conocido; filtros y Excel concuerdan, reversión sin doble conteo.|Integración/manual, F8|
|P13|RF21, RNF04|Acciones críticas auditadas; buscar ausencia de contraseñas/tokens en logs y exportaciones.|Integración/revisión, F4–F9|
|P14|RF25, RNF06|Respaldar y recuperar en entorno aislado; comprobar filas, relaciones y sumas; medir tiempo y pérdida posible.|Operación, F2/F9|
|P15|RNF01|Desconectar Internet manteniendo LAN: vender; caída de API: error claro sin venta local pendiente.|Manual, F10|
|P16|RNF03|Invocar API directamente sin permiso o con turno ajeno; 401/403, sin cambio de datos. Probar CORS, CSRF y SQL malicioso como entrada.|Integración, F4–F10|
|P17|RNF05|Medir p95 con 3 cajas y 10 000 productos en equipo documentado.|Carga, F10|
|P18|RNF08/11|Teclado, impresión y validación; límites de día Managua/UTC y redondeos aprobados.|Unidad/manual, F7–F10|

## Datos y enfoque

Datos sintéticos: tres roles, dos cajas, tres cajeros, stock cero/uno/múltiple, producto inactivo, precios con fracciones y ventas en límites de fecha. Nunca copiar credenciales reales. Probar consultas Dapper, bloqueos, CHECK y FK en MySQL 8.4 real; un mock no demuestra concurrencia de base de datos. Pruebas unitarias para cálculos e invariantes; integración para transacciones/permisos; recorrido manual completo para venta, cierre y recuperación.

Antes de aceptar cada fase: guardar escenario, versión, datos, resultado esperado/real y evidencia; resolver fallos críticos antes de continuar. Fase 1 solo permite revisión documental y aprobación; ejecutar código/instalar herramientas no forma parte de su aceptación.

## Revisión documental de esta entrega

Verificar presencia de ≥15 RF (hay 25), RNF, ≥10 tablas relacionadas, ocho reportes, tres gráficos, endpoints, capas, Docker y decisiones abiertas. Revisar enlaces locales y consistencia de turno/venta/inventario. No se declaran compilación, pruebas funcionales o restauración exitosas.

## Cobertura incorporada Fase 6

Véase [[Verificación Fase 6]]: pruebas automatizadas de clasificación, productos/proveedores, ubicación múltiple, precisión, ajustes, idempotencia, no negativos, traslados atómicos, concurrencia, alertas y autorización. Pendientes: recorrido visual autenticado y medición LAN bajo carga. El detalle por ubicación se compara con saldos globales e historial mediante Verify.

## Identidad del negocio

[[Verificación identidad del negocio]] registra validaciones de nombre/lema, lectura autenticada, edición exclusiva de Administrador con permiso, HTTPS, versión obsoleta, persistencia MySQL y auditoría atómica. Incluye revisión visual con datos simulados. Pendientes: recorrido real en varias terminales LAN y propagación de una sesión ya abierta mediante recarga.

## Inventario operativo

Resultados ejecutados y límites en [[Verificación inventario operativo]]. El script database/Test-InventoryOperations.ps1 ejecuta la suite completa en una base temporal con todas las migraciones. Cubre atomicidad multilínea, no negativos, reintentos, permisos, concurrencia, conteos y regresión POS. Pendiente recorrido real entre terminales LAN.
