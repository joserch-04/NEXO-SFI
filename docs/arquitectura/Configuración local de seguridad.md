# Configuración local de seguridad

Enlaces: [[Fase 4]], [[Verificación Fase 4]]. Esta nota registra la instalación posterior a las pruebas de implementación.

## Configuración aplicada

- Clave JWT aleatoria de 48 bytes, guardada en secretos de usuario y en `appsettings.Local.json` privado e ignorado por Git. Se conservó la conexión existente de nexo_app. No se incluye ningún secreto en esta nota.
- Certificado HTTPS de desarrollo de localhost instalado como confiable en Windows. Solo cubre este equipo; un despliegue LAN necesita su certificado correspondiente.
- Administrador `admin`, nombre Administrador NEXO, creado mediante SecurityService.Bootstrap y PasswordHasher, sin habilitar las cuentas demo.
- Contraseña aleatoria con una copia cifrada mediante DPAPI para la cuenta actual de Windows en `%LOCALAPPDATA%\Nexo\administrador-inicial.xml`, fuera del repositorio. En MySQL solo se conserva el hash de Identity. El archivo cifrado no es portable a otra cuenta de Windows.
- Perfil HTTPS colocado primero en launchSettings.json. API local iniciada con HTTPS en https://localhost:7080 y health HTTP en http://localhost:5080. Se reinició exclusivamente el proceso Nexo.Api de este proyecto. El proceso en segundo plano termina al cerrar sesión o reiniciar Windows; no se instaló un servicio de inicio automático.

Para consultar la contraseña en una terminal PowerShell de la misma cuenta de Windows:

```powershell
(Import-Clixml "$env:LOCALAPPDATA\Nexo\administrador-inicial.xml").GetNetworkCredential().Password
```

Después de cambiar la contraseña desde la aplicación, la copia inicial ya no corresponderá a la contraseña vigente. La aplicación no lee ese archivo para autenticar: usa el hash de MySQL.

Para ejecutar desde Visual Studio, seleccionar el perfil **https**. Si la API en segundo plano sigue usando los puertos 7080/5080, detener esa instancia antes de iniciar una segunda. Desde terminal, ejecutar desde NEXO:

```powershell
dotnet run --project backend/Nexo.Api --launch-profile https
```

## Ajuste del comando de instalación

`Program.cs` conserva la lectura interactiva oculta y admite entrada estándar redirigida para bootstrap. La instalación suministró usuario, nombre y contraseña mediante stdin de un proceso hijo, sin contraseña en argumentos, código fuente ni archivos de texto plano. El comando continúa siendo exclusivamente local y rechaza crear otro administrador si ya existe uno activo.

## Verificación ejecutada

Compilación Release correcta, sin errores ni advertencias de compilación; el SDK del equipo sigue siendo preliminar. Se verificaron por HTTP real, contra MySQL local:

1. Certificado HTTPS validado por Windows, sin omitir errores de certificado.
2. health/live = alive y health/ready = ready.
3. Login con la credencial cifrada de admin.
4. auth/me con rol Administrador y consulta autorizada de usuarios.
5. Refresh con cookie real del cliente HTTP.
6. Logout y rechazo 401 del access token emitido antes del cierre.

No se imprimieron contraseñas, claves JWT ni tokens en la salida de verificación. Estas comprobaciones amplían la evidencia de [[Verificación Fase 4]]; todavía no son una prueba del frontend ni de otra terminal de la LAN.
