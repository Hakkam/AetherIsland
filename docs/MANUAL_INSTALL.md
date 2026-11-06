# Manual install / run guide

## Run without installing anything

1. Get `AetherIsland-Portable-V2-win-x64.zip` (built locally under
   `release/portable-v2/` or from GitHub Actions artifacts).
2. Right-click → Extract All… → choose a folder you can write to
   (not `C:\Program Files`).
3. Double-click `AetherIsland.exe`. If SmartScreen appears:
   More info → Run anyway (the binary is unsigned).
4. The island appears near the top of your main display as a small pill.

## Run the manual ZIP helper

`Run-AetherIsland.cmd` inside the package simply starts the exe — handy for
pinning to the taskbar.

## Install per-user (no admin)

Double-click `AetherIsland-Setup-win-x64.exe` → Next → Install. Files go to
`%LOCALAPPDATA%\Programs\AetherIsland`, Start Menu shortcuts are created and
an uninstall entry appears in Windows Settings → Apps.

## Verify your install

- Tray icon (Aether Island) appears near the clock.
- Ctrl+Alt+Space toggles the island.
- Settings shows "Launch at Windows startup" — tick it to start with Windows.
- `Data\` appears next to the exe (portable) or install folder (installed).

## Remove

- Portable: exit from the tray, delete the folder.
- Installed: Windows Settings → Apps → Aether Island → Uninstall. Choose
  whether to keep your `Data\` folder when asked.
