# Aether Island Portable V2

## Operating principle

The portable application is a self-contained Windows 10/11 x64 WPF executable set,
built from `src/AetherIsland.Portable`. No installer, app registration or
user admin rights are necessary. Extract the full ZIP to a **writable** folder.

Launch `AetherIsland.exe`. The default state is a compact top pill.
A short click expands, mouse drag moves it, and the expanded top rail supports
dragging as well. The window never auto-collapses on focus loss.
Use the right-click system tray menu to collapse, reset, configure or exit.
Mouse icons use Segoe MDL2 Assets supplied by Windows.

## Included verified-feature code paths

- UI interactions: Dashboard, screen-time, media session, shelf, tasks,
  notes, focus, live weather, actual Windows network state, AI hard limit.
- Real Windows shell shortcuts lead to Windows Settings or launch real OS apps;
  they are not represented as unimplemented Wi-Fi/Bluetooth toggles.
- Weather is not displayed until a geocoded location has been chosen.
- Screen time is opt-in and records coarse app foreground aggregates at
  ~10-second intervals; never window titles or keystrokes.
- Media uses Windows SMTC with actual playback controls and live metadata.
- AI: only OpenAI **organization API** hard monthly limit, when the
  documented Admin API proves enforcement and costs; no ChatGPT, Codex,
  Claude or other unsupported subscription quota cards.
- Credentials: Windows DPAPI CurrentUser encryption and local storage.

## Portable storage

`./Data/settings.json`, `tasks.json`, `notes.json`, `shelf.json`,
`screen-time.json`, `cache/`, and `logs/` are created on demand. The
application must have write access to the extraction directory. The secret
store is DPAPI-bound to the Windows account: moving to a different Windows
user requires re-entering credentials.

## Build and output

On Windows with .NET SDK 10, run:

```powershell
./tools/publish-portable-v2.ps1
```

It writes `release/portable-v2/AetherIsland-Portable-V2-win-x64.zip`
and a matching `.sha256` digest. Bundle is self-contained but multi-file,
minimizing the extraction overhead of single-file bundles.

## Performance choices

No always-on fullscreen or telemetry polling. The clock refreshes every
minute; media subscribes lazily on first access; focus updates every second
only while running; activity tracking remains off until explicitly enabled
and then samples every 10 seconds with a batched save every minute. Network
and weather are fetched only when requested and can fail with truthful
error/empty states. No third-party UI frameworks or browser processes.

## Validation limits

Building and static source review do **not** prove UI fidelity or low CPU
usage on a laptop. Before treating this as production-ready, physically
launch the portable EXE on Windows; verify mouse press/release and drag,
state changes, navigation, screenshot visual match to the supplied references,
multi-DPI and multi-display positioning, SMTC compatibility, network and
weather, credential authorization, resource use and sleep/resume behavior.
The source-only milestones and Windows CI artifacts are distinct from
interactive visual and functional acceptance.

## One-click preview instead of downloading ZIP every iteration

The distribution now contains `Preview-AetherIsland.cmd` and `Preview-AetherIsland.ps1` beside `AetherIsland.exe`. Install GitHub CLI (`winget install --id GitHub.cli -e`) and run `gh auth login` **once**. Afterwards, exit the running Aether Island from the Windows notification area and double-click `Preview-AetherIsland.cmd` whenever you want the newest **successful** GitHub Actions preview. The script selects the most recent successful Windows run, downloads and checks SHA-256, updates only application files (never `Data/`), records the preview ID and launches it. When unchanged, it just launches the existing executable.

A source-build alternative is running the portable project through Visual Studio with the .NET 10 SDK; no separate ZIP is needed for each edit.

The opt-in `--ui-selfcheck` command-line mode exercises WPF expansion/collapse and each vector navigation button on the Windows runner. Passing that check does not certify matching pixels or real mouse/DPI behavior on a laptop.
