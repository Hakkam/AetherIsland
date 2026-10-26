param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\Aether Island'),
    [switch]$SkipShortcuts,
    [switch]$DesktopShortcut
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PSScriptRoot)
$target = [IO.Path]::GetFullPath($Destination)
$exe = Join-Path $source 'AetherIsland.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Extract the complete manual package first, then run this installer beside AetherIsland.exe.'
}
if ($source.TrimEnd('\') -eq $target.TrimEnd('\')) {
    throw 'The install destination must differ from the extracted package.'
}
# Prevent replacing in-use binaries during manual reinstallation.
$running = @(Get-CimInstance Win32_Process -Filter "Name='AetherIsland.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and [string]::Equals($_.ExecutablePath, (Join-Path $target 'AetherIsland.exe'), [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) { throw 'Exit Aether Island from the system tray before updating its files.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Set-Content -LiteralPath (Join-Path $target '.aether-manual-install') -Value 'Aether Island manual installation' -Encoding utf8
if (-not $SkipShortcuts) {
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Aether Island.lnk'))
    $shortcut.TargetPath = Join-Path $target 'AetherIsland.exe'
    $shortcut.WorkingDirectory = $target
    $shortcut.Save()
    if ($DesktopShortcut) {
        $desktop = [Environment]::GetFolderPath('Desktop')
        $desktopShortcut = $shell.CreateShortcut((Join-Path $desktop 'Aether Island.lnk'))
        $desktopShortcut.TargetPath = Join-Path $target 'AetherIsland.exe'
        $desktopShortcut.WorkingDirectory = $target
        $desktopShortcut.Save()
    }
}
Write-Host "Installed Aether Island at $target"
Write-Host "Run $(Join-Path $target 'AetherIsland.exe')"
