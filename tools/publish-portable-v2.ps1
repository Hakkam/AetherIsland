param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/AetherIsland.Portable/AetherIsland.Portable.csproj'
$release = Join-Path $root 'release/portable-v2'
$output = Join-Path $release 'win-x64'
if (Test-Path -LiteralPath $release) { Remove-Item -LiteralPath $release -Force -Recurse }
New-Item -ItemType Directory -Path $output -Force | Out-Null
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Portable V2 publish failed' }
$exe = Join-Path $output 'AetherIsland.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Portable executable was not created' }
Set-Content -LiteralPath (Join-Path $output 'portable.flag') -Value 'Aether Island Portable V2'
@'
AETHER ISLAND PORTABLE V2 — Windows 10/11 x64
Extract the entire ZIP before using it. Double-click AetherIsland.exe.
No .NET Desktop Runtime installation or administrator rights are needed.
Settings, notes, tasks, and other application data live in Data beside this EXE.
The destination folder must be writable. Moving the folder to another Windows
account requires re-entering OpenAI Admin API credentials; credentials are
encrypted using Windows DPAPI and are not transferable.
Right-click the notification area icon for Exit or Reset position.
No update is applied automatically. Remove the folder to uninstall.
'@ | Set-Content -LiteralPath (Join-Path $output 'README-PORTABLE.txt')
$zip = Join-Path $release 'AetherIsland-Portable-V2-win-x64.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($zip + '.sha256') -Value "$hash  AetherIsland-Portable-V2-win-x64.zip" -NoNewline
if (!(Test-Path -LiteralPath ($zip + '.sha256'))) { throw 'Missing checksum' }
Write-Host "Portable V2: $zip"
Write-Host "SHA256: $hash"
