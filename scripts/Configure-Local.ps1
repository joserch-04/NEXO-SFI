 $ErrorActionPreference = 'Stop'
 $projectRoot = Split-Path $PSScriptRoot -Parent
 $webRoot = Join-Path $projectRoot 'frontend/nexo-web'
 Push-Location $webRoot
 try {
     if (!(Test-Path '.env.local')) { Copy-Item '.env.example' '.env.local' }
     New-Item -ItemType Directory -Force '.certs' | Out-Null
     & dotnet dev-certs https --trust
     if ($LASTEXITCODE -ne 0) { throw 'No se pudo confiar en el certificado HTTPS.' }
     & dotnet dev-certs https --export-path '.certs/localhost.pem' --format PEM --no-password
     if ($LASTEXITCODE -ne 0) { throw 'No se pudo exportar el certificado HTTPS.' }
     if (!(Test-Path '.certs/localhost.key')) { throw 'No se encontró la clave PEM exportada.' }
     Write-Host 'Configuración HTTPS lista. Frontend: https://localhost:5173; API: https://localhost:7080.'
 } finally { Pop-Location }
