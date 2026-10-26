param(
    [string]$Repository = 'Hakkam/AetherIsland',
    [string]$Branch = 'AetherIsland',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$destination = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $destination 'AetherIsland.exe'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('AetherIsland-Preview-' + [Guid]::NewGuid().ToString('N'))
$artifact = 'AetherIsland-Portable-V2-win-x64'
try {
    if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is required once. Install with: winget install --id GitHub.cli -e ; reopen the preview window afterward."
    }
    & gh auth status --hostname github.com *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Sign in once with: gh auth login' }

    $raw = & gh run list --repo $Repository --branch $Branch --workflow windows-release.yml --status success --limit 1 --json databaseId
    if ($LASTEXITCODE -ne 0) { throw 'Could not list successful Windows preview builds.' }
    $runs = @($raw | ConvertFrom-Json)
    if ($runs.Count -eq 0) { throw 'No successful Windows preview exists yet.' }
    $run = [string]$runs[0].databaseId
    $currentFile = Join-Path $destination '.aether-preview-run'
    $current = if (Test-Path -LiteralPath $currentFile) { (Get-Content -LiteralPath $currentFile -Raw).Trim() } else { '' }
    if (!$Force -and $current -eq $run -and (Test-Path -LiteralPath $exe)) {
        Write-Host "Already on preview build $run. Launching the existing app..."
        Start-Process -FilePath $exe -WorkingDirectory $destination
        return
    }
    if (Test-Path -LiteralPath $exe) {
        $active = @(Get-Process -Name AetherIsland -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and [string]::Equals($_.Path, $exe, [StringComparison]::OrdinalIgnoreCase) })
        if ($active.Count -gt 0) {
            throw 'Exit Aether Island from its system-tray menu, then double-click Preview-AetherIsland.cmd again. Your Data folder is preserved.'
        }
    }
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    Write-Host "Downloading successful preview run $run ..."
    & gh run download $run --repo $Repository --name $artifact --dir $temp
    if ($LASTEXITCODE -ne 0) { throw "Could not download artifact $artifact from run $run." }
    $zip = Join-Path $temp 'AetherIsland-Portable-V2-win-x64.zip'
    $checksumFile = $zip + '.sha256'
    if (!(Test-Path $zip) -or !(Test-Path $checksumFile)) { throw 'ZIP or SHA256 checksum is missing.' }
    $expected = ((Get-Content -LiteralPath $checksumFile -Raw).Trim() -split '\s+')[0]
    $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    if (![string]::Equals($actual, $expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The downloaded archive failed SHA256 verification. No files were updated.'
    }
    $staging = Join-Path $temp 'unpacked'
    Expand-Archive -LiteralPath $zip -DestinationPath $staging -Force
    if (!(Test-Path (Join-Path $staging 'AetherIsland.exe')) -or
        !(Test-Path (Join-Path $staging 'portable.flag'))) {
        throw 'The extracted build is not a valid Aether Island Portable package.'
    }

    # Update binaries and scripts only; never replace private Data.
    Get-ChildItem -LiteralPath $staging -Force | Where-Object { $_.Name -ne 'Data' } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
    Set-Content -LiteralPath $currentFile -Value $run -NoNewline
    Write-Host "Updated Aether Island to preview $run. Local Data has not been touched."
    Start-Process -FilePath $exe -WorkingDirectory $destination
}
catch {
    Write-Host "Preview update failed: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force -Recurse -ErrorAction SilentlyContinue }
}
