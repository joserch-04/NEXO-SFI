$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$taskSuffix=[Guid]::NewGuid().ToString('N')
$taskVolume="nexo_f7_images_test_$taskSuffix"
$taskContainer="nexo_f7_images_test_$taskSuffix"
$taskImage=(docker compose images -q mysql).Trim()
if(-not $taskImage){throw 'Se necesita la imagen MySQL local; ejecutar primero Docker Compose.'}
$fixture='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQaQr4DwADegHm+SuGZwAAAABJRU5ErkJggg=='
$expected=[Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([Convert]::FromBase64String($fixture))).ToLowerInvariant()
function Check-Exit {if($LASTEXITCODE -ne 0){throw 'Falló prueba Docker de persistencia de imágenes.'}}
function Read-ImageHash {
    $result=(docker exec $taskContainer sha256sum /data/product-images/prueba.png) -join ''
    Check-Exit
    if($result.Split(' ')[0] -ne $expected){throw 'La fotografía no conserva su contenido.'}
}
try {
    docker volume create $taskVolume | Out-Null;Check-Exit
    docker run -d --name $taskContainer --network none --read-only --mount "type=volume,src=$taskVolume,dst=/data/product-images" --entrypoint sh $taskImage -c 'sleep 300' | Out-Null;Check-Exit
    docker exec $taskContainer sh -c 'umask 077; printf "%s" "$1" | base64 -d > /data/product-images/prueba.png' sh $fixture;Check-Exit
    Read-ImageHash
    docker restart $taskContainer | Out-Null;Check-Exit
    Read-ImageHash
    docker rm -f $taskContainer | Out-Null;Check-Exit
    docker run -d --name $taskContainer --network none --read-only --mount "type=volume,src=$taskVolume,dst=/data/product-images" --entrypoint sh $taskImage -c 'sleep 300' | Out-Null;Check-Exit
    Read-ImageHash
    Write-Host 'PNG conserva SHA-256 después de reiniciar y recrear contenedor con volumen persistente.'
} finally {
    # Solo recursos efímeros de esta ejecución, identificados por UUID propio.
    docker rm -f $taskContainer 2>$null | Out-Null
    docker volume rm $taskVolume 2>$null | Out-Null
}
