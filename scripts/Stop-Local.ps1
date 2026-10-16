$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
foreach ($taskPort in @(5173, 7080)) {
    $connections = Get-NetTCPConnection -LocalPort $taskPort -State Listen -ErrorAction SilentlyContinue
    foreach ($taskProcessId in @($connections.OwningProcess | Sort-Object -Unique)) {
        if (!$taskProcessId) { continue }
        $taskProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $taskProcessId"
        if (!$taskProcess) { continue }
        $belongsToProject = ($taskProcess.ExecutablePath -and $taskProcess.ExecutablePath.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) -or
            ($taskProcess.CommandLine -and $taskProcess.CommandLine.IndexOf($projectRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        if (!$belongsToProject) { throw "El puerto $taskPort pertenece a un proceso que no se pudo identificar como NEXO. No se detuvo." }
        Stop-Process -Id $taskProcessId
        Write-Host "Servidor NEXO del puerto $taskPort detenido."
    }
}
