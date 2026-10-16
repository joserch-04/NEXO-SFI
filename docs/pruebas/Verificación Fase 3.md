# Verificación Fase 3

Fecha: 15 de octubre de 2026. Enlaces: [[Fase 3]], [[Plan de pruebas]], [[Verificación Fase 2]].

## Entorno observado

Windows, SDK 11.0.100-rc.2.26504.105; destino de todos los proyectos net10.0, runtime ASP.NET Core 10.0.13. No hay SDK 10 estable instalado: queda pendiente repetir la compilación con ese SDK. No se cambió el destino a .NET 11.

Docker Compose informó nexo-mysql-1 healthy, MySQL 8.4.11, puerto 127.0.0.1:13306. La prueba real usó nexo_app y la credencial local existente, solo en una variable del proceso retirada al terminar. No se guardaron contraseñas en los archivos del backend ni pruebas.

## Resultado final ejecutado

Comando desde NEXO, con NEXO_TEST_MYSQL configurada temporalmente:

```powershell
dotnet test backend/Nexo.sln --no-restore --verbosity minimal -m:1
```

Código de salida 0. Los seis proyectos compilaron para net10.0.

|Suite|Correctas|Fallidas|Omitidas|
|---|---:|---:|---:|
|Nexo.UnitTests|3|0|0|
|Nexo.IntegrationTests|10|0|0|
|Total|13|0|0|

Las pruebas unitarias verificaron traducción de disponibilidad y propagación de cancelación. Nueve casos HTTP con WebApplicationFactory verificaron live sin acceso a datos, ready 200/503/500, errores genéricos sin el texto sensible inyectado, traceId, ruta inexistente 404, OpenAPI con ambas rutas, ausencia de OpenAPI en Production y CORS permitido/rechazado. El décimo caso de integración ejecutó SELECT parametrizado mediante Dapper y MySqlConnector contra MySQL real.

Las pruebas HTTP usan servidor en memoria y sustituyen IDatabaseProbe. La conexión MySQL se probó separadamente con la implementación real; no se declara un recorrido de navegador ni un despliegue HTTPS verificado. Si NEXO_TEST_MYSQL no está definida, el caso MySQL se omite explícitamente.

## Incidencias y solución

- Inicialización de dotnet y acceso a Docker bloqueados por permisos del entorno: se ejecutaron con autorización.
- NU1903 por Microsoft.OpenApi 2.0.0 transitivo: se actualizaron Microsoft.AspNetCore.OpenApi y Microsoft.AspNetCore.Mvc.Testing a 10.0.13. La restauración posterior pasó sin esa advertencia; no se suprimió la auditoría.
- Las primeras pruebas HTTP fallaron porque la cadena se comprobaba antes de aplicar ConfigureAppConfiguration del servidor de pruebas. La comprobación se movió después de Build y la fábrica resuelve IConfiguration mediante DI. El arranque sigue rechazando una cadena ausente.
- Dos ejecuciones solapadas disputaron DLL e informaron agotamiento de memoria/host abortado. Se repitió la suite completa de forma secuencial con -m:1; resultado final 13/13.

## Límites y pendientes

No se probaron módulos de negocio, autenticación, autorización, transacciones de venta, carga ni concurrencia: no forman parte de esta fase. Quedan para sus etapas. El sondeo no comprueba versión del esquema ni integridad de tablas. El certificado y la configuración LAN de producción quedan pendientes. No había repositorio Git inicializado; no se creó commit.

## Referencias técnicas consultadas

- [Problem Details y manejo de errores de ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- [MySqlConnector con Dapper](https://mysqlconnector.net/tutorials/dapper/).
- [Opciones de conexión y pooling](https://mysqlconnector.net/connection-options/).
## Ajuste de arranque local en Visual Studio

El usuario informó nuevamente la excepción de conexión ausente. Se comprobó que el secreto existe para el mismo usuario que ejecuta Visual Studio; no se mostró su valor. No se pudo observar el entorno efectivo del proceso fallido de Visual Studio, por lo que la causa exacta queda sin confirmar.

Se fijó ActiveDebugProfile=http en el archivo local Nexo.Api.csproj.user (ignorado por Git) y se añadieron explícitamente DOTNET_ENVIRONMENT=Development y ASPNETCORE_ENVIRONMENT=Development al perfil http de launchSettings.json. No se cambió Program.cs ni se guardaron credenciales en el repositorio.

Verificación ejecutada: dotnet run --no-build --project backend/Nexo.Api --launch-profile http --urls http://127.0.0.1:15089. El log confirmó Hosting environment: Development y GET /api/v1/health/ready respondió status ready usando los secretos de usuario. Se detuvo únicamente el proceso temporal de comprobación. Falta confirmar el arranque desde la interfaz de Visual Studio tras recargar el proyecto.

## Corrección posterior: ejecución directa Debug sin perfil

El cambio de perfil no resolvió el error comunicado por el usuario. Se modificó Program.cs para recuperar explícitamente secretos solo en DEBUG cuando falta la cadena: primero AddUserSecrets<Program>, después la ubicación del perfil Windows independiente de APPDATA. Release conserva su comportamiento anterior. El mensaje de error incluye entorno y perfil de usuario, sin credenciales.

Pruebas posteriores: dotnet test backend/Nexo.sln --no-restore --verbosity minimal -m:1 terminó con código 0: 3 unitarias y 9 HTTP correctas, una MySQL omitida por no definir NEXO_TEST_MYSQL en esa ejecución. Adicionalmente se arrancó directamente backend/Nexo.Api/bin/Debug/net10.0/Nexo.Api.exe, sin launchSettings, con ambas variables de entorno en Production y APPDATA apuntando a una ubicación sin secretos. El proceso arrancó y /api/v1/health/ready respondió ready contra MySQL real. Se restauraron las variables del proceso y se detuvo el ejecutable temporal. Esto prueba el respaldo de secretos del binario Debug; no equivale a observar el depurador de Visual Studio. No se confirmó la causa exacta del arranque fallido previo.

## Resolución del 503 persistente en la instancia del puerto 5080

MySQL se encontraba healthy y las credenciales de ambas ubicaciones de secretos coincidían. Una prueba Dapper con esos secretos pasaba, mientras el proceso existente del puerto 5080 devolvía 503; no se confirmó qué cadena conservaba ese proceso. Se añadió configuración privada appsettings.Local.json exclusiva de Debug, ignorada por Git y excluida de publicación, y diagnóstico numérico seguro en el sondeo. Se restauró la validación de arranque y el middleware de errores, cuya condición estaba incompleta en el archivo encontrado.

La primera compilación falló por DLL bloqueada por Nexo.Api PID 22060. Se validó su ruta dentro de este proyecto y se detuvo esa instancia. La repetición de dotnet test backend/Nexo.sln --no-restore --verbosity minimal -m:1 terminó con código 0: tres unitarias y nueve HTTP correctas, una MySQL opcional omitida. Se inició la versión recompilada con perfil http en localhost:5080 y se comprobó la URL exacta /api/v1/health/ready: HTTP 200 con status ready contra MySQL real. La API quedó ejecutándose en segundo plano para permitir al usuario recargar la vista previa. Antes de volver a ejecutarla desde Visual Studio se debe detener esa instancia para liberar el puerto y los archivos.
