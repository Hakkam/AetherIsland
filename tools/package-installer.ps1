param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'publish-win-x64.ps1') -SelfContained:$SelfContained
if ($LASTEXITCODE -ne 0) { throw 'Publishing the application failed.' }

$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($compiler) {
    $compilerPath = $compiler.Source
} else {
    $known = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $compilerPath = $known | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $compilerPath) {
        throw 'Install Inno Setup 6 and make ISCC.exe available to package the installer.'
    }
}
& $compilerPath (Join-Path $root 'installer/AetherIsland.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $root 'release/AetherIsland-Setup-win-x64.exe'
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer output not found.' }
Write-Host $setup
