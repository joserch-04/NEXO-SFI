# NEXO

Inventario y facturación académica para tienda de conveniencia. Fase actual: backend base ASP.NET Core 10; frontend pendiente.

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
