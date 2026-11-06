# Builds release/AetherIsland-Setup-win-x64.exe with Inno Setup 6.
# - Publishes the self-contained portable layout first (same output as the ZIP).
# - Compiles installer/AetherIsland.iss with ISCC.exe (installed via
#   "winget install JRSoftware.InnoSetup" when missing).
# Requires: .NET 10 SDK on Windows. Produces an unsigned setup exe.

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

& "$repo\tools\publish-portable-v2.ps1" @args
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 not found. Install with: winget install --id JRSoftware.InnoSetup -e"
}

$version = (Select-Xml -Path "$repo\src\AetherIsland.Portable\AetherIsland.Portable.csproj" -XPath "/*/*[local-name()='Version']").Node.InnerText
$env:AETHER_VERSION = $version

& $iscc /Q "$repo\installer\AetherIsland.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setup = "$repo\release\AetherIsland-Setup-win-x64.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
"$hash  $(Split-Path -Leaf $setup)" | Set-Content "$setup.sha256"
Write-Host "Installer: $setup"
Write-Host "SHA256:    $hash"
