# Verificación identidad del negocio

Fecha: 16 de octubre de 2026. Relacionada con [[Identidad y configuración del negocio]], [[Fase 6]] y [[Plan de pruebas]].

## Resultados ejecutados

|Verificación|Resultado|
|---|---|
|Build backend Release net10.0|Correcto; 0 errores y 0 advertencias de compilación. El SDK instalado 11 RC emite el aviso informativo NETSDK1057.|
|Suite completa backend con base MySQL aislada|15 unitarias + 70 integración correctas; 0 fallidas y 0 omitidas.|
|Suite frontend existente|10/10 correctas. Corresponde al cliente de sesión; no se afirma que automatice el formulario nuevo.|
|TypeScript/Vite|Compilación correcta, 46 módulos.|
|Migración en la instalación existente|006 aplicada; perfil inicial Mi tienda, sin nombre real del cliente.|
|Instalación desde cero|Las seis migraciones y las dos semillas se aplicaron en una base temporal; integridad SQL correcta en MySQL 8.4.11.|
|Revisión de interfaz|Vista previa, edición de nombre/lema y guardado con actualización del encabezado verificados en navegador mediante datos simulados.|

## Cobertura nueva

- BusinessProfileTests: nombre vacío, espacios, caracteres de control en nombre/lema, longitudes máximas, normalización, persistencia a través del contrato y conflicto de versión.
- BusinessProfileApiTests: GET anónimo 401; lectura de operador autenticado 200 y no-store; PUT rechazado con 403 para Consulta, Administrador sin permiso y Consulta con permiso; Administrador con permiso guarda; entrada inválida 400; versión antigua 409; HTTP rechazado con 400 tanto para leer como para editar.
- MySqlBusinessProfileTests: guardado real con usuario nexo_app, lectura desde otra instancia del repositorio, incremento de versión, rechazo de versión antigua, registro de auditoría y rollback ante actor inexistente. Se verifica que el perfil y el número de eventos auditados no cambian después del fallo. Finalmente se restaura el perfil inicial de ensayo.
- Las pruebas HTTP del módulo usan un esquema de autenticación exclusivo del proyecto de pruebas para explorar la matriz de autorización. La suite de seguridad existente continúa comprobando JWT, revocación y permisos.

## Aislamiento y datos

Las pruebas reales se ejecutaron contra `nexo_identidad_verificacion_20261016`, creada exclusivamente para el ensayo con permisos de aplicación equivalentes a los del instalador: auditoría/movimientos solo lectura e inserción, tablas puente con DELETE y sin DDL para nexo_app. Se eliminaron esa base temporal y sus datos al terminar correctamente. No se cambió el nombre comercial en la base nexo real; allí únicamente se aplicó la migración y su perfil genérico.

La comprobación visual utilizó un arnés temporal de frontend: introdujo Tienda El Encuentro y Siempre cerca de vos, verificó que la vista previa cambiaba antes de guardar y que el encabezado tomaba el nombre solo tras guardar. Las respuestas fueron simuladas; no se enviaron credenciales ni cambios al MySQL real. Los dos archivos del arnés se retiraron al finalizar. La imagen de comprobación es un ejemplo, no el perfil de un cliente real.

## Cómo repetir

```powershell
pwsh -NoProfile -File database/Manage-Database.ps1 Migrate
# Para pruebas MySQL, establecer NEXO_TEST_MYSQL en entorno privado con una base de ensayo.
dotnet test backend/Nexo.sln -c Release
npm --prefix frontend/nexo-web test
npm --prefix frontend/nexo-web run build
```

Sin NEXO_TEST_MYSQL los casos MySqlFact se omiten explícitamente. Ejecutar las pruebas de persistencia sobre datos de ensayo, ya que crean registros auditados y restauran el nombre original al terminar.

## Incidencias y límites

- Vite requirió ejecución autorizada fuera del sandbox para leer la configuración; no se alteró su política de seguridad.
- La verificación del backend se hizo en Release para conservar la API Debug abierta por el usuario. Reiniciar y recompilar la API del IDE antes de usar las rutas nuevas.
- No se realizaron pruebas reales de operación desde varias terminales LAN ni de despliegue. La detección de versión obsoleta sí se comprobó por HTTP y MySQL; no se ejecutó una carrera entre dos navegadores.
- No hay propagación en tiempo real a otras sesiones abiertas: toman los datos al iniciar sesión o recargar.
- No se implementaron logo, colores ni integración fiscal. El nombre y lema son identidad de presentación del negocio.

## Ampliación: icono en imagen

- Suite completa contra base temporal nueva con migraciones 001–007: **24 unitarias y 70 de integración aprobadas**, 0 fallidas y 0 omitidas. La base `nexo_icono_verificacion_20261016` se eliminó después del resultado correcto. La instalación nexo recibió solamente la migración 007; no se le asignó una imagen real.
- BusinessIconTests: PNG válido, null para quitar, dimensiones cero/excedidas, formatos no admitidos (incluido SVG), URL externa, base64 inválido, corrupción CRC, truncado, exceso de tamaño y datos descomprimidos insuficientes. El fixture es un PNG RGBA de un píxel generado para ensayo.
- Prueba HTTP del Administrador: carga válida, lectura con imagen, rechazo de icono SVG y eliminación con null. Se conservan los ensayos de permisos, HTTPS y versión.
- MySqlBusinessProfileTests: imagen persistida y leída desde otra instancia, retirada y confirmación de null; restauración del perfil de prueba al terminar.
- Frontend: build TypeScript/Vite correcto (47 módulos); suite existente 10/10 aprobada.
- Navegador: selector de archivos real con PNG de prueba, conversión canvas, vista previa, guardado simulado que muestra el icono en el encabezado, Quitar imagen y guardado que lo retira. El arnés y el archivo de prueba se eliminaron; no se utilizó una imagen del cliente ni se modificó la base real mediante el navegador.
- No se comprobó visualmente por separado la decodificación de archivos JPG/WebP ni la operación desde varias terminales LAN. No se afirma ese recorrido como ejecutado.

La captura ilustra un icono verde sintético de ensayo. Para usarlo, reiniciar/recompilar API e iniciar sesión como Administrador; abrir Configuración, elegir imagen y guardar.
