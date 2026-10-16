# Ejecución local IDE

Configuración aplicada el 15 de octubre de 2026. Relacionado: [[Fase 5]], [[Configuración local de seguridad]], [[Verificación Fase 5]].

## Configuración del equipo

Se creó `frontend/nexo-web/.env.local` con URL API HTTPS 7080 y rutas de certificado `.certs/localhost.pem` / `.certs/localhost.key`. Se exportó el certificado de desarrollo existente de .NET y se comprobó que Windows confía en él. La clave privada, variables locales y registros `.local/` están ignorados por Git. No se alteraron conexión MySQL, clave JWT ni credencial administrativa existente.

`scripts/Configure-Local.ps1` permite repetir la preparación: conserva `.env.local` si existe, confía en el certificado y exporta PEM para Vite. Requiere .NET instalado y puede solicitar confirmación de Windows. No agrega secretos al código.

## Visual Studio 2026 y VS Code juntos

1. Docker Desktop debe estar abierto.
2. Si hay servidores NEXO ejecutándose en segundo plano, desde NEXO ejecutar `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Local.ps1`. Solo detiene procesos identificados como este proyecto en 5173/7080. Conserva MySQL.
3. Abrir `backend/Nexo.sln` en Visual Studio 2026, establecer Nexo.Api como inicio, seleccionar https y pulsar F5.
4. Abrir `NEXO.code-workspace` en VS Code. Menú Terminal → Ejecutar tarea → `NEXO: MySQL`, y luego `NEXO: frontend`.
5. Abrir https://localhost:5173 y utilizar la cuenta existente. No ejecutar otra instancia de API al mismo tiempo.

## Todo desde VS Code

Abrir `NEXO.code-workspace`, ejecutar la tarea `NEXO: iniciar todo`. Inicia frontend y API en terminales dedicadas; API depende del inicio saludable de MySQL. Las dependencias npm ya están instaladas; para una instalación nueva usar npm ci en frontend/nexo-web.

Para depurar la API con F5, instalar la extensión C# recomendada y elegir `NEXO: depurar API HTTPS`. Iniciar primero MySQL y detener cualquier instancia previa de API. Para depurar React, iniciar el frontend y elegir `NEXO: depurar frontend` (Microsoft Edge). Se validó el JSON de configuraciones; no se probó una sesión interactiva de depuración dentro de los IDE.

## Comprobación real

Se ejecutó docker compose up -d --wait y MySQL quedó Healthy. Se iniciaron API y Vite en segundo plano, con ventanas ocultas y registros privados en `.local`. La primera consulta coincidió con la compilación y recibió conexión rechazada; al completar el arranque se repitió y se obtuvo HTTP 200 en:

- https://localhost:7080/api/v1/health/live
- https://localhost:7080/api/v1/health/ready
- https://localhost:5173

Las solicitudes validaron el certificado sin omitir errores TLS. Esto confirma arranque, servicio de frontend y conexión API/MySQL; no sustituye las pruebas de login y navegación real en navegador pendientes de [[Verificación Fase 5]].

Los procesos en segundo plano de esta sesión no son servicios permanentes. Al reiniciar Windows, volver a iniciar mediante las tareas o los IDE.
