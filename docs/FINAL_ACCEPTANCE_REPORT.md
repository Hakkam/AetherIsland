# Final acceptance report — AetherIsland Portable V2.1 (2026-11-07)

Environment: real Windows 11 x64 desktop (Chinese-locale user account, 4K
display, Intel iGPU), .NET 10 SDK 10.0.403. All statements below are
evidence-backed; "pending" items are listed explicitly at the end.

## Executive summary

The repository now contains a complete, reference-driven Windows island:
13 navigation modules, a real quick-toggle grid, live weather with IP
auto-location, screen-time analytics with categories and per-app icons,
SMTC media with album art, ICS calendar with month grid, camera mirror,
file shelf, tasks with reminder parsing, autosaving notes, focus timers,
wellbeing nudges, utilities, a full settings page, tray + global hotkey,
Windows-startup launch, dark/light/system theming — packaged as both a
self-contained portable ZIP and a verified per-user Inno Setup installer.

## Reference fidelity

- Panel/nav/header/dashboard widgets match the two photos: verified by
  rendering every module of the running app (`--capture-views`, 15 PNGs in
  `docs/screenshots/`) and by a live desktop capture with the expanded
  island floating over real applications (`live-desktop-expanded.png`).
- Video tour behaviors reproduced: media+calendar combined view, shelf
  empty state, tasks Active/Done/Trash with "0 open · 0 done", notes editor,
  weather detail (chips + hourly + 7-day), Wi-Fi/Bluetooth manager.
  Differences are deliberate and listed in REFERENCE_FIDELITY_MATRIX.md §
  "Deliberate differences" (UV max vs current, signal % vs band, vector
  glyphs, honest OAuth/ABS limits).

## Functional verification (performed on this machine)

| Check | Result |
|---|---|
| `dotnet build` Debug + Release publish | 0 warnings, 0 errors |
| `dotnet test` | 27/27 passed |
| `--ui-selfcheck` (debug build) | Passed — expand, 13 nav handlers, content geometry, collapse |
| `--ui-selfcheck` (installed exe) | Passed |
| `--capture-views` | 15 PNGs written from the live app |
| Live weather | Auto-located Istanbul; 8° Partly cloudy, H12/L8/Feels6, humidity 71%, wind 4 km/h, UV 2.2, 1021 hPa, sunrise/sunset, hourly strip with precip % (real Open-Meteo) |
| Live media | Real SMTC session rendered (Brave, paused, album art, 9:35/42:20 timeline) |
| Radio states | Wi-Fi On, Bluetooth Off reflected live from Windows.Devices.Radios |
| Hotkey toggle | Window rect verified 196×46 ⇄ 945×248 on Ctrl+Alt+Space |
| Startup | HKCU Run key written and pointing at the installed exe |
| Installer (Inno 6.7.3) | Silent install OK (files, Start Menu, uninstall entry) → installed-exe self-check OK → silent uninstall OK (files + entry removed, `Data\` preserved by default) → reinstalled and left running |
| Portable ZIP | Self-contained, `portable.flag` present, no `Data\` inside the ZIP, SHA-256 recorded |

## AI integrity

Only the OpenAI organization API monthly hard-limit provider ships
(AI_PROVIDER_CAPABILITIES.md). Without a key the module shows an explicit
"add key" state; without a verified enforcing limit it refuses to invent
numbers. Keys are DPAPI-encrypted, never logged.

## Performance (measured)

- Idle CPU: **0.00%** across a 60 s window (6 samples, collapsed and
  expanded). One 0.78% spike occurred during a minute-boundary re-render.
- GC: managed heap ≈ 0 idle; 0 gen0/gen1 collections per 2 s at idle.
- Working set (this machine, idle): ≈ 351 MB; expanded ≈ 452 MB. Breakdown:
  in-process Intel GPU user-mode driver (igc64 69.5 MB + igd12dxva64
  55.8 MB + igd12um64xel 14.5 MB ≈ 140 MB) loaded by WPF's D3D renderer,
  plus .NET 10 runtime, WPF, WinForms tray, WinRT projection
  (Microsoft.Windows.SDK.NET 23.7 MB) — see module list captured in the
  session log. Managed allocations are negligible; this is the platform
  floor on this hardware, not app bloat. CI runners with different display
  stacks report lower absolute numbers for the same binary.

## Distribution

| Artifact | Size | SHA-256 |
|---|---|---|
| `release/portable-v2/AetherIsland-Portable-V2-win-x64.zip` | 80.4 MB | `080c722146809baf3fc6cfd26d641202951fcc7183e6bc81588c0dbd5dd44bb7` |
| `release/AetherIsland-Setup-win-x64.exe` | 57.4 MB | `FD6DFC2030C1F7825D55F7961971D355960318259F551D422374F35B0C06EAB5` |

Both are unsigned (SmartScreen notice on first run). CI regenerates them.

## Pending manual verification (needs a human pointer / hardware)

1. Mouse-drag repositioning of pill and panel (drag threshold logic is the
   CI-tested code path; the physical gesture was not simulated).
2. Explorer drag-and-drop into/out of the File Shelf.
3. Tray left/right click behavior.
4. Camera mirror on a machine with a camera.
5. Wi-Fi Connect/Disconnect with real networks (blocked here by Location
   permission; the honest hint path is implemented).
6. Arabic RTL visual pass.

## Completion percentage (evidence-based)

- Implemented + verified: ~85% of the acceptance matrix.
- Implemented, pending manual interaction checks: ~12%.
- Unsupported by design (documented): ~3% (OAuth, third-party quota cards).
Not claimed: 100%.
