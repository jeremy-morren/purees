function GetAuthHeader {
    param (
        [string]$Verb,
        [string]$ResourceType,
        [string]$ResourceLink,
        [string]$Date
    )
    $masterKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="

    $payload = "$($Verb.ToLowerInvariant())`n$($resourceType.ToLowerInvariant())`n$($resourceLink)`n$($date.ToLowerInvariant())`n`n"

    $sha256 = [System.Security.Cryptography.HMACSHA256]::new()
    $sha256.Key = [Convert]::FromBase64String($masterKey)

    $hashPayload = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))

    $signature = [Convert]::ToBase64String($hashPayload)

    $keyType = "master";
    $tokenVersion = "1.0";
    [Uri]::EscapeDataString("type=${keyType}&ver=${tokenVersion}&sig=${signature}")
}

function CheckRunning {
    $ErrorActionPreference = 'Continue'
    curl.exe --insecure "https://localhost:8081/_explorer/index.html" --max-time 1 -sSf 1>$null 2>$null
    
    if ($LASTEXITCODE -ne 0) {
        return $false
    }
    
    $date = [Datetime]::UtcNow.ToString("ddd, dd MMM yyyy HH':'mm':'ss 'GMT'")
    $apiVersion = "2015-08-06"
    
    curl.exe --insecure "https://localhost:8081/dbs" --max-time 5 -sSf  `
        -H "Authorization: $(GetAuthHeader -Verb 'GET' -ResourceType "dbs" -Date $date)" `
        -H "x-ms-version: ${apiVersion}" `
        -H "x-ms-date: ${date}" `
        1>$null 2>$null
    
    return $LASTEXITCODE -eq 0
}

function CheckAdmin {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Wait()
{
    param (
        [int]$Timeout = 300
    )

    #Start-CosmosDBEmulator is unreliable

    $start = [DateTime]::Now

    $lastTick = [DateTime]::Now

    while (([DateTime]::Now - $start).TotalSeconds -lt $Timeout) {
        if (CheckRunning) {
            Write-Host "Cosmos emulator started"
            return 0
        }

        if (([DateTime]::Now - $lastTick).TotalSeconds -ge 10) {
            Write-Host "Waiting..."
            $lastTick = [DateTime]::Now
        }
    }

    Write-Error "Timed out waiting for emulator to start"
    return 1
}

if (CheckRunning) {
    Write-Host "Azure Cosmos emulator already running"
}
elseif (CheckAdmin)
{
    Write-Host "Starting cosmos emulator (admin)"

    #Start the emulator
    Import-Module "$env:ProgramFiles\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator"

    $key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="

    Stop-CosmosDBEmulator
    Start-CosmosDBEmulator -NoFirewall -NoUI -AllowNetworkAccess -Key $key -PartitionCount 100 -NoWait

    #Using -Wait doesn't work
    #So we wait manually
    Wait
}
else {
    Write-Host "Starting cosmos emulator"

    #Run current script with admin rights
    Start-Process powershell.exe -ArgumentList ('-File', $PSCommandPath) -Verb runas

    #Using -Wait doesn't work
    #So we wait manually
    Wait
}
