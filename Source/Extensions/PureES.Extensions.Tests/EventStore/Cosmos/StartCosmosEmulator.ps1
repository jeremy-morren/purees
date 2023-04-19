$name = 'cosmos-emulator'

curl.exe --insecure "https://localhost:8081/" -sS 1>$null 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Emulator already running" -ForegroundColor Cyan
    exit
}

& (Join-Path $env:ProgramFiles "Docker\Docker\DockerCli.exe") -SwitchWindowsDaemon

docker stop $name
docker rm $name

$id = docker run -d --rm --tty `
    --platform "windows/amd64" `
    --publish '8081:8081/tcp' `
    --publish '10250-10255:10250-10255/tcp' `
    --memory 4g --cpus=4.0 `
    --name $name `
    --env AZURE_COSMOS_EMULATOR_PARTITION_COUNT=5 `
    --env AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=false `
    mcr.microsoft.com/cosmosdb/windows/azure-cosmos-emulator

if ($LASTEXITCODE -ne 0) {
    return
}

Write-Host "Created container, waiting for emulator to start" -ForegroundColor Cyan

while ($true) {
    docker container inspect $id 1>$null 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Container exited prematurely"
        break
    }

    curl.exe --insecure "https://localhost:8081/" -sS 1>$null 2>$null

    if ($LASTEXITCODE -eq 0) {
        break
    }

    Start-Sleep -Seconds 1
}

Write-Host "Started emulator. ID $($id.Substring(10))" -ForegroundColor Cyan