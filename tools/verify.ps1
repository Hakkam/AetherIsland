$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $sdks = @(dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0 -or -not ($sdks | Where-Object { $_ -match '^10\.' })) {
        throw '.NET 10 SDK is required. dotnet --list-sdks returned no compatible SDK.'
    }
    dotnet restore AetherIsland.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build AetherIsland.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test AetherIsland.slnx -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & (Join-Path $PSScriptRoot 'publish-win-x64.ps1')
    if (-not (Test-Path -LiteralPath (Join-Path $root 'release/win-x64/AetherIsland.exe'))) {
        throw 'Published executable is missing.'
    }
    Write-Host 'Automated build, tests, and publish succeeded. Interactive acceptance remains required.'
}
finally { Pop-Location }
