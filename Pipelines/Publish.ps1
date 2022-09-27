Param(
    [Parameter(Mandatory)][string]$Project
)

$project = Join-Path $PSScriptRoot (Join-Path "../src" $project)

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) "PureES"

Remove-Item $tmp -Recurse -ErrorAction Ignore
New-Item $tmp -ItemType Directory

try {
    Push-Location $tmp -StackName publish
    
    $version = [DateTime]::UtcNow.toString("HHmm")
    dotnet pack $project -c Release -o (Get-Location) "-p:Version=0.1.$version"
}
finally {
    Pop-Location -StackName publish
    #Remove-Item $tmp
}