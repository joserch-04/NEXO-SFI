# Verificación Fase 6

Fecha: 16 de octubre de 2026. Véanse [[Fase 6]], [[Manual catálogo e inventario]], [[Plan de pruebas]].

## Resultados ejecutados

|Verificación|Resultado|
|---|---|
|Compilación Release solución ASP.NET Core net10.0|Correcta; 0 errores y 0 advertencias de compilación. El host selecciona SDK 11 RC instalado y emite aviso NETSDK1057; el target continúa siendo net10.0.|
|Suite completa con NEXO_TEST_MYSQL y usuario nexo_app|9 unitarias + 63 integración correctas; 0 fallidas, 0 omitidas.|
|Frontend npm test|10/10 correctas (cliente de sesión existente).|
|Frontend npm run build, tras los últimos cambios de interfaz|TypeScript y Vite correctos; 44 módulos.|
|Migración 005 sobre la instalación existente|Aplicada y checksum registrado; saldos previos conservados en B-01.|
|SeedCatalog|55 productos DEMO-F6 cargados; 11 categorías solicitadas, 11 subcategorías, 5 preparados sin control.|
|Manage-Database.ps1 Verify|Correcto: stock global/libro, detalle por ubicación/libro, totales de venta y permisos MySQL.|
|Concurrencia real|Dos salidas distintas sobre 10 unidades: una confirma y otra devuelve insufficient_stock; saldo final cero.|
|Rollback|Traslado de 11 cuando hay 10 revierte; conserva 10 y no deja movimientos parciales.|
|Autorización HTTP|8 rutas de lectura rechazan anónimo con 401 y usuario sin permiso específico con 403; 9 rutas POST rechazan privilegios insuficientes con 403.|
|Lectura HTTP autorizada con BD real|Categorías, subcategorías, proveedores, ubicaciones, productos, detalle, saldos, alertas e historial responden 200; parámetros fuera de rango responden 400.|

La prueba de lectura HTTP usa autenticación de prueba que concede permisos para comprobar contratos; las pruebas de seguridad separadas ejercitan JWT real, revocación y permisos. No representa inicio de sesión completo del navegador.

## Casos específicos de catálogo

`MySqlCatalogTests` verifica altas de maestros, FK de clasificación mediante servicio, relaciones producto/proveedor, consulta por SKU/categoría/subcategoría, producto nuevo agotado, entrada, traslado, saldos 30/10, reintento idéntico, conflicto por clave reutilizada, insuficiencia, bloqueo de desactivación de ubicación con saldo, ciclos, ajuste negativo, salida, retiro de proveedor, cambio a producto sin inventario, exclusión de alertas, rechazo de movimientos sin inventario, versión obsoleta, historial de traslado con suma cero y desactivación.

La mayoría de estos datos se revierten al finalizar la prueba. El ensayo de concurrencia confirma operaciones reales con un producto `TEST-<UUID>`, que termina inactivo y con saldo cero; su historial y auditoría permanecen por trazabilidad. Si se interrumpe el ensayo, la siguiente ejecución recupera únicamente sus fixtures identificados por nombre y prefijo. Se confirmó que no quedan productos TEST activos.

`InventoryRulesTests` verifica precisión de cantidades, no negativos y conservación del total en traslado. La suite de cliente frontend no verifica los formularios nuevos; estos pasaron compilación de TypeScript y empaquetado, y sus servicios fueron ejercitados por HTTP/MySQL.

## Cómo repetir

```powershell
# Base local preparada y migrada. Configurar NEXO_TEST_MYSQL fuera del repositorio
# con el usuario nexo_app; no imprimir contraseña ni guardarla en documentación.
dotnet test backend/Nexo.sln -c Release
npm --prefix frontend/nexo-web test
npm --prefix frontend/nexo-web run build
pwsh -NoProfile -File database/Manage-Database.ps1 Verify
```

Sin NEXO_TEST_MYSQL, los casos MySqlFact se omiten explícitamente: no contarlos como aprobados. El caso HTTP de detalle `/productos/1` y Verify presuponen el dataset inicial; para usar las pruebas de instalación completas preparar también Seed en la base exclusiva de ensayo. SeedCatalog puede usarse con un administrador bootstrap sin cargar el dataset anterior.

## Incidencias corregidas

- CHECK no admitido sobre columna autoincremental de MySQL: se retiró, se inspeccionó la aplicación parcial y se completó el tramo faltante. No se reejecutó DDL previo ni se descartaron datos.
- Dapper no materializaba records con IDs UNSIGNED/COALESCE: se añadieron constructores sin parámetros a las vistas de saldo e historial. Las lecturas reales y HTTP posteriores pasaron.
- Infrastructure no puede depender de Application: error de catálogo definido en Domain, traducido por GlobalExceptionHandler.
- Compilación Debug bloqueada por API abierta: se verificó Release sin detener el IDE del usuario.
- Restricciones de sandbox de Vite, Docker y EventLog: pruebas repetidas con permiso de ejecución. El primer intento de suite dentro del sandbox falló en la prueba de logging de error; la ejecución final completa pasó fuera del sandbox.
- Dos compilaciones de pruebas solapadas encontraron una DLL bloqueada. Se dejaron terminar y se repitió la suite secuencialmente.
- Se corrigió el estado del formulario al navegar entre tipos de maestros y se preservan proveedores inactivos ya asociados durante edición.

## Pendientes y límites

- Revisión visual y recorrido manual de los formularios autenticados (escritorio y móvil): no había sesión de NEXO disponible en el navegador conectado. No se afirma validación visual.
- Prueba entre terminales reales LAN y medición de rendimiento bajo carga. El bloqueo conservador serializa las operaciones de catálogo.
- Integración con confirmación de venta y comprobantes de Fase 7. Los preparados están listos como datos de venta sin stock obligatorio; aún no hay flujo de facturación funcional.
- Reiniciar/recompilar la API abierta en el IDE para que cargue los endpoints nuevos.

## Instalación desde cero verificada

Se creó una base temporal aislada en MySQL 8.4.11, se aplicaron en orden las migraciones 001–005 y las semillas 001_demo y 002_catalogo_demo, y se ejecutaron las aserciones de integridad. Resultado: correcto, con 55 productos DEMO-F6. La base temporal se eliminó al terminar; la base nexo existente se conservó.

Esta prueba detectó y corrigió la colisión histórica de IDs de permisos entre Seed y las migraciones de seguridad: la semilla inicial ahora resuelve permisos por código y conserva los ya creados. No se modificó ningún script de migración anterior a 005.

## Verificación de la reorganización de Inventario

- TypeScript y Vite compilan correctamente; 45 módulos.
- Suite frontend existente: 10/10 correctas. Son pruebas del cliente de sesión; no se presentan como pruebas automatizadas de los paneles nuevos.
- Recorrido en navegador con un arnés temporal y respuestas simuladas, sin credenciales y sin modificar MySQL: pestañas Existencias/Movimientos/Alertas, contador de alertas, historial separado y filtros plegados, apertura del formulario lateral, escritura de cantidad y motivo, cierre y reapertura conservando ambos valores, selección de traslado y ubicaciones, envío simulado y mensaje de confirmación, detalle con total 40 distribuido como 10/30, cierre con Escape y devolución del foco al disparador.
- Se inspeccionó visualmente la vista estrecha y se añadió ancho mínimo de tabla con desplazamiento horizontal para evitar encabezados amontonados. El arnés temporal se retiró al terminar.
- Limitación: este recorrido comprueba interacción y presentación con datos simulados. No se repitió una operación real contra MySQL desde el nuevo panel ni un ensayo LAN; la suite real de backend documentada anteriormente corresponde a las mismas rutas, que no cambiaron.
- Incidencias: Vite requirió ejecución fuera del sandbox para leer su configuración. TypeScript detectó que Stock usa productId en lugar de id al preseleccionar un producto; se corrigió el mapeo antes de compilar y revisar la interfaz.
