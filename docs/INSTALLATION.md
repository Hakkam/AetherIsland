# Installation

## Option A — installer (recommended for daily use)

1. Download `AetherIsland-Setup-win-x64.exe` (from `release/` locally, or the
   GitHub Actions artifact `AetherIsland-Setup-win-x64`).
2. Run it. No admin rights required (per-user install into
   `%LOCALAPPDATA%\Programs\AetherIsland`).
3. Optionally tick the desktop-icon task. The Start Menu gets
   **Aether Island** and an uninstaller.
4. The app launches; enable **Launch at Windows startup** in Settings (on by
   default) so the island starts with Windows.

Upgrades: run the newer setup; it closes a running instance and replaces
files. User data (`Data\`) is preserved.

Uninstall: Windows Settings → Apps → Aether Island → Uninstall (or Start →
Uninstall Aether Island). It stops the app, removes files and shortcuts and
the optional startup entry, then asks whether to also delete your `Data\`
folder (default: keep).

## Option B — portable (no install)

1. Download `AetherIsland-Portable-V2-win-x64.zip` (+ `.sha256`).
2. Extract **completely** to a writable folder (e.g. `D:\Tools\AetherIsland`).
3. Open `AetherIsland.exe`. Everything (settings, notes, tasks, screen-time,
   logs, weather cache) lives in `Data\` next to the exe — see
   PORTABLE_USAGE.md.

Notes:
- Self-contained: no .NET runtime install needed. Windows 10 1809+ / 11 x64.
- Unsigned binaries may show SmartScreen ("More info → Run anyway").
- Startup-on-login for the portable build: Settings → "Launch at Windows
  startup" (writes the standard per-user Run key pointing at that folder; the
  path self-heals if you move the folder).

## Build from source

```powershell
# portable ZIP (self-contained)
./tools/publish-portable-v2.ps1

# installer (publishes first, then compiles installer/AetherIsland.iss)
winget install --id JRSoftware.InnoSetup -e   # once
./tools/package-installer.ps1
```

Outputs: `release/portable-v2/AetherIsland-Portable-V2-win-x64.zip` (+SHA-256)
and `release/AetherIsland-Setup-win-x64.exe` (+SHA-256).
