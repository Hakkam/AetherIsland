# Portable usage

## What "portable" means here

- The extracted folder is the whole application. Run `AetherIsland.exe`.
- **All** user data is created inside `Data\` next to the exe:
  - `settings.json` — theme, accent, position, schedule, weather, consents
  - `tasks.json`, `notes.json`, `shelf.json` — your content
  - `screen-time.json` — rolling 7-day local activity aggregates
  - `credentials.bin` — DPAPI-encrypted API keys (OpenAI admin key)
  - `cache\<lat_lon>\weather.json` — per-location weather cache
  - `screenshots\`, `logs\app.log` (+ rotated previous log)
- No registry writes for data, no services, no scheduled tasks. The single
  exception you opt into: the standard per-user startup Run key
  (`HKCU\...\CurrentVersion\Run\AetherIsland`) when "Launch at Windows
  startup" is enabled. It stores the exe path and self-heals when the folder
  moves.

## Moving the folder

1. Exit from the tray menu.
2. Move the folder; run the exe again. Position/size adapt; if the saved
   position is off-screen it snaps back into the visible work area.
3. Credentials note: DPAPI protects keys for **this Windows account**, so a
   move to another PC or another Windows user requires re-entering the AI
   API key (Settings → AI quota). Everything else moves cleanly.

## Daily use

- Click the pill to expand; click the ✕ or press Esc to collapse (unless
  pinned); Ctrl+Alt+Space toggles from anywhere; drag the header to move.
- Tray icon: left-click toggles, right-click has Open / Refresh / Settings /
  Launch at startup / Reset position / Exit.
- Data safety: never share or sync `Data\credentials.bin`; delete
  `screen-time.json` or use "Delete history" to erase activity aggregates.

## One-click preview (developers)

`Preview-AetherIsland.cmd` (copied next to the exe by the publish script)
pulls the newest successful GitHub Actions build with `gh` and updates the
application files in place — never touching `Data\`.
