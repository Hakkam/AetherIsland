param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'src/AetherIsland.App/AetherIsland.App.csproj'
$sdk = dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdk | Select-String '^10\.')) {
    throw 'Install the .NET 10 SDK on Windows before publishing Aether Island.'
}
$output = Join-Path $projectRoot 'release/win-x64'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output -Force | Out-Null
if ($SelfContained) {
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $output
} else {
    dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o $output
}
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
foreach ($file in @('Install-AetherIsland.ps1', 'Install-AetherIsland.cmd', 'Run-AetherIsland.cmd', 'Uninstall-AetherIsland.ps1', 'Uninstall-AetherIsland.cmd')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "tools/$file") -Destination (Join-Path $output $file) -Force
}
Write-Host "Published to $output"
