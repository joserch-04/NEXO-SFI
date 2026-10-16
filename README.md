# NEXO

Inventario y facturación académica para tienda de conveniencia. Fase actual: frontend base React/TypeScript sobre la API ASP.NET Core 10.

La documentación en español está en [docs/README.md](docs/README.md). Instrucciones de instalación y resultados reales: [Fase 2](docs/base-de-datos/Fase%202.md).

Requisitos de esta fase: Docker Desktop con contenedores Linux, Docker Compose y PowerShell 7.

```powershell
Copy-Item .env.example .env
# Editar .env: establecer dos contraseñas distintas y no vacías.
docker compose up -d --wait
pwsh -File database/Manage-Database.ps1 Migrate
pwsh -File database/Manage-Database.ps1 Seed
pwsh -File database/Manage-Database.ps1 Verify
```

`Seed` es opcional y exclusivo de desarrollo; ejecutarlo una vez en base vacía. `Verify` requiere ese dataset. `Migrate` valida checksums y omite versiones aplicadas. No editar migraciones ya aplicadas: agregar la siguiente numerada. No ejecutar dos instalaciones simultáneas.

La instalación verificada usa `MYSQL_PORT=13306` en `.env`; el ejemplo propone 3306. MySQL queda publicado solo en loopback, base `nexo`, usuario `nexo_app`. Obtener contraseña de `.env`; la API futura no usa root. Ejecutar migraciones con el script administrativo, no con el usuario de aplicación. No conectar la aplicación antes de completar `Migrate`, que reduce los permisos iniciales creados por la imagen.

`docker compose stop` detiene el servicio conservando datos. `docker compose up -d --wait` lo reanuda. El volumen no sustituye un respaldo; no usar `down -v` sobre datos que se necesiten conservar. Cambiar `.env` no cambia contraseñas en un volumen ya inicializado.


## Backend base (Fase 3)

Solución: backend/Nexo.sln. Configuración, ejecución, capas y endpoints: [Fase 3](docs/arquitectura/Fase%203.md). Resultados: [Verificación Fase 3](docs/pruebas/Verificación%20Fase%203.md).

Desde esta carpeta: `dotnet test backend/Nexo.sln`. Configurar ConnectionStrings:MySql mediante secretos de usuario antes de ejecutar `dotnet run --project backend/Nexo.Api --launch-profile http`.

## Seguridad y usuarios (Fase 4)

Login, JWT de 10 minutos, refresh rotatorio revocable en cookie segura, gestión de usuarios, roles y políticas de permisos. Configuración de clave JWT, HTTPS y administrador inicial: [Fase 4](docs/arquitectura/Fase%204.md). Evidencia: [Verificación Fase 4](docs/pruebas/Verificación%20Fase%204.md).

Aplicar `pwsh -File database/Manage-Database.ps1 Migrate`, configurar `Jwt:SigningKey` fuera del repositorio y usar el perfil `https`. No hay contraseñas predeterminadas; crear administrador con `dotnet run --project backend/Nexo.Api --launch-profile https -- --bootstrap-admin`. Pruebas: `dotnet test backend/Nexo.sln -c Release`; MySQL requiere `NEXO_TEST_MYSQL`.

## Frontend base (Fase 5)

Aplicación en `frontend/nexo-web`: React, TypeScript, Vite y Tailwind. Login, navegación adaptable, rutas protegidas por sesión/permisos, consulta de usuarios y cambio de contraseña. JWT en memoria y refresh mediante cookie HttpOnly; requiere HTTPS.

Instalación, certificados y ejecución: [Fase 5](docs/arquitectura/Fase%205.md). Resultados y pendientes: [Verificación Fase 5](docs/pruebas/Verificación%20Fase%205.md). Desde `frontend/nexo-web`: `npm ci`, configurar `.env.local` y certificado según la guía, `npm run dev`. Validación: `npm test` y `npm run build`.
