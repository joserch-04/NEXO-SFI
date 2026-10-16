# Verificación Fase 4

Fecha: 15 de octubre de 2026, America/Managua. Enlaces: [[Fase 4]], [[Verificación Fase 3]], [[Plan de pruebas]].

## Resultado ejecutado

**41 pruebas correctas, 0 fallidas y 0 omitidas**: 3 en Nexo.UnitTests y 38 en Nexo.IntegrationTests. Compilación Release con destino net10.0. SDK disponible: 11.0.100-rc.2.26504.105; sigue pendiente repetir con SDK .NET 10 estable. No se cambió el destino de los proyectos.

Comando: `dotnet test backend/Nexo.sln -c Release`, con `NEXO_TEST_MYSQL` establecido temporalmente desde la configuración privada local, sin imprimir la cadena y retirándolo del entorno al terminar. Sin esa variable se omiten explícitamente las dos pruebas MySQL; los demás casos no requieren servidor.

Se aplicó `pwsh -File database/Manage-Database.ps1 Migrate` contra el MySQL local. El script reconoció como ya aplicadas 001 y 002 y completó 003 con los privilegios limitados de aplicación. Las migraciones anteriores y el dataset demo no se editaron.

## Cobertura real

- Login, cookie Secure/HttpOnly/SameSite Strict y consulta de identidad con JWT real.
- Rotación de refresh, detección de reutilización y revocación de la familia, incluyendo access tokens ya emitidos.
- Logout y rechazo posterior del access token.
- Firma, emisor, audiencia y expiración de JWT incorrectos: 401. Cada prueba obtiene primero una sesión real válida, comprueba su acceso y conserva su familia vigente al alterar la condición bajo prueba.
- Sin autenticación: 401 en las 16 rutas administrativas, incluidos métodos de escritura.
- Sin permiso: 403; retirar permisos en el almacenamiento cambia la autorización sin reemitir JWT.
- Creación de usuario sin exponer hash, gestión de roles y asignación de permisos; contraseñas almacenadas mediante PasswordHasher.
- Un usuario con permiso de lectura puede consultar usuarios pero no desactivarlos.
- Protección del último administrador y rechazo del cambio de roles propios: 409 y reversión de la operación.
- Cambio de contraseña propia revoca sesiones; usuario desactivado no puede iniciar sesión.
- Ausencia de cabecera CSRF, Origin ajeno, transporte HTTP, credenciales incorrectas y límite de solicitudes.
- Dos refresh simultáneos: solo uno rota; la reutilización posterior revoca la familia. Esta prueba usa el doble transaccional, no conexiones MySQL simultáneas.
- Refresh vencido y access token de esa familia rechazados.
- Bootstrap rechazado cuando ya hay administrador activo; contraseña débil rechazada; ausencia de ruta pública de registro.
- Pruebas anteriores de health, errores, CORS y OpenAPI conservadas.

## MySQL real

Se ejecutaron dos casos con el usuario de aplicación: el sondeo parametrizado de Fase 3 y `MySqlSecurityTests.Security_repository_and_services_work_with_real_mysql_and_rollback_test_data`.

El segundo crea usuarios y un rol temporales dentro de una transacción exterior. Usa el repositorio MySQL real y SecurityService para verificar mapeo, hash de contraseña, permisos, login, sesión, rotación, reutilización, cambio de contraseña y desactivación. Comprueba los IDs de permisos asignados al rol. Finalmente lanza una excepción de control para revertir todo y verifica desde otra transacción que el usuario temporal no existe. No crea una cuenta permanente ni deja un rol de prueba confirmado. Los contadores AUTO_INCREMENT pueden avanzar aunque la transacción se revierta; esto es normal en MySQL.

Las pruebas HTTP usan WebApplicationFactory con un doble en memoria que conserva exclusión mutua y rollback. Así se verifican middleware, JWT Bearer, políticas, filtros y respuestas sin mezclar datos productivos. La prueba MySQL no usa HTTP y no sustituye las verificaciones pendientes de navegador o despliegue LAN.

## Incidencias resueltas

1. NuGet.Config no era legible dentro del entorno restringido; se autorizó restaurar JWT Bearer.
2. La API Debug abierta bloqueó ensamblados durante la compilación. Se verificó Release conservando el proceso abierto; para usar el código nuevo se debe reiniciar la API después de configurar los secretos.
3. Dapper recibió Guid al leer CHAR(36) de MySqlConnector. Se convirtió `familia` a CHAR(100) en la consulta para obtener texto. La prueba real pasó tras la corrección.
4. Una prueba modificaba BaseAddress después de enviar solicitudes. Se cambió a una URL absoluta para verificar HTTP.
5. Se corrigió una referencia de namespace en los DTO de pruebas y se eliminaron blancos finales detectados por revisión del diff.

## Límites y pasos de instalación

No se probó todavía la sesión desde React, un navegador real o distintas terminales LAN. No se verificó concurrencia real de varias instancias MySQL ni carga sostenida. La serialización global de seguridad y el límite por IP/proceso están descritos en [[Fase 4]].

La instalación final requiere configurar una clave JWT privada, un certificado confiable y crear el administrador mediante el comando local interactivo de [[Fase 4]]. No se ejecutó ese diálogo ni se estableció una contraseña permanente durante esta verificación. El frontend corresponde a Fase 5. No se realizaron commits ni despliegue.

## Verificación posterior de instalación

Se completó la configuración privada, se creó admin y se comprobó la API HTTPS real, incluyendo login, permisos, refresh, logout y rechazo 401 tras revocación. Véase [[Configuración local de seguridad]]. Los límites históricos anteriores corresponden a la verificación de implementación previa a esa instalación.
