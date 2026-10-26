param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\Aether Island'),
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Destination)
$marker = Join-Path $target '.aether-manual-install'
if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
    throw 'This path is not identified as an Aether Island manual installation. Refusing to delete it.'
}
$exePath = Join-Path $target 'AetherIsland.exe'
$running = @(Get-CimInstance Win32_Process -Filter "Name='AetherIsland.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and [string]::Equals($_.ExecutablePath, $exePath, [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) {
    throw 'Exit Aether Island from the system tray before uninstalling.'
}
foreach ($shortcut in @(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Aether Island.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Aether Island.lnk')
)) {
    if (Test-Path -LiteralPath $shortcut) {
        $shell = New-Object -ComObject WScript.Shell
        $link = $shell.CreateShortcut($shortcut)
        if ([string]::Equals($link.TargetPath, $exePath, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $shortcut -Force
        }
    }
}
Remove-Item -LiteralPath $target -Recurse -Force
if ($RemoveUserData) {
    Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AetherIsland') -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'Aether Island was uninstalled. User data was preserved unless -RemoveUserData was specified.'
