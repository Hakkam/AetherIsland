param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'src/AetherIsland.App/AetherIsland.App.csproj'
$sdk = dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdk | Select-String '^10\.')) {
    throw 'Install the .NET 10 SDK on Windows before publishing Aether Island.'
}
$output = Join-Path $projectRoot 'release/win-x64'
if ($SelfContained) {
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
} else {
    dotnet publish $project -c Release --self-contained false -p:PublishSingleFile=false -o $output
}
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host "Published to $output"
