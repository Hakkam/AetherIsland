# Aether Island — Reference-to-implementation audit (2026-10-26)

Target branch: `AetherIsland` in `Hakkam/AetherIsland`.

## Evidence and limits

- Primary UI reference: two user screenshots and a short reference video showing a top-center floating black island, compact navigation, weather, quick system actions, app launcher, day progress, file shelf, notes, media/calendar, Wi-Fi/Bluetooth, screen-time analytics, and extended weather.
- Product specification: 27-section master prompt including a native Windows shell and AI Usage Center.
- Code review: WPF application and associated projects, settings, integration adapters, release workflow, and Inno Setup source.
- Automated Windows workflow run [40272645652](https://github.com/Hakkam/AetherIsland/actions/runs/40272645652): succeeded on source revision `2b5e0880d54f9003a880cef0ae6119b5e1eb1fea`, reporting 27/27 tests and packaging two artifacts.
- The previous automated result does not validate real installation, Windows interaction, layout fidelity, provider credentials, or physical hardware. New code changes require a new successful workflow result.

## Feature-level comparison

| Reference/prompt feature | Source evidence | Acceptance status |
| --- | --- | --- |
| Compact island, expansion/collapse, pinning, hotkeys, tray, monitor/fullscreen behavior | `IslandWindow`, `ShellViewModel`, `HotkeyService`, `TrayIconService` | Implemented; desktop interaction unverified |
| Four-card reference dashboard; reordering/visibility | `DashboardView`, `DashboardViewModel`, Settings layout controls | Implemented; pixel-level reference review pending |
| File shelf and Windows drag-and-drop | `FileShelfView`, corresponding ViewModel and SQLite repository | Implemented; drag/drop acceptance pending |
| Notes, tasks, focus timers, wellbeing reminders | Views/ViewModels and local repositories | Implemented; persistence and timer/suspend acceptance pending |
| Weather / forecasts | Open-Meteo adapter, weather View | Implemented; live location/network acceptance pending |
| Media controls, local camera mirror | Windows media session watcher; user-activated MediaCapture | Implemented; supported player/camera device acceptance pending |
| Calendar | ICS adapter and OAuth PKCE adapters for Google/Microsoft | Implemented; external account sign-in/consent pending |
| Screen-time and system analytics | Windows foreground tracker, idle detection, metric readers | Implemented; real-life idle/DPI/battery acceptance pending |
| Wi-Fi and Bluetooth | Native Wi-Fi integration and Windows enumeration/status views | Implemented in part; hardware/device action acceptance pending |
| OpenAI verified API hard spend limits | Official organization usage/cost/spend-limit APIs; Admin key | Implemented; account-level validation pending |
| Anthropic, Z.AI, subscription quota display | No verified provider quota integration meeting project restriction | Intentionally excluded; no synthetic quotas |
| Local agent detection | Consent-gated process/session metadata detector | Implemented; observed statuses do not prove provider subscription limits |
| Arabic/English/Turkish | JSON localized resources and right-to-left flow | Implemented in part; complete RTL/accessibility audit pending |
| Windows Setup and portable application | Windows GitHub Actions + Inno Setup | Automated output verified for prior revision; manual install and unsigned-binary acceptance pending |

## Engineering fixes added after the prior successful build

- `AiUsageService`: serialize refreshes to prevent overlapping billing/usage API polling; deduplicate quota alerts within a monthly reporting window.
- `AppServices`: connect computed quota alerts to actual tray and compact-island notifications.
- `ScreenTimeService`: subtract detected inactive time rather than attributing it to the foreground app; split long sessions at midnight instead of charging the new day for the previous day's activity.
- `ShellViewModel`: provide a dashboard fallback if users hide every navigation module, avoiding an empty-list exception.
- `README.md`: removed stale contradictions about OAuth/calendar, camera preview and actual installer availability.

## Remaining mandatory acceptance

1. Confirm latest commit passes .NET 10 Windows Release build and unit tests, with `AetherIsland-Setup-win-x64.exe` and self-contained portable ZIP attached.
2. Install through Inno Setup on a real Windows 10/11 x64 desktop; verify first launch, tray close/exit, uninstaller, and preserved private user data.
3. Exercise every module on Windows, including persistence across restart, keyboard-only access and reduced motion.
4. Compare the four-widget dashboard and island transitions to the actual media at 100% and 150% Windows DPI, with Arabic RTL, light/dark themes, and multi-monitor/fullscreen windows.
5. Verify Wi-Fi/Bluetooth controls against the available hardware; verify audio sessions and user-approved camera capture/stop.
6. Authorize a real Google/Microsoft calendar desktop OAuth application; confirm consent and token refresh, then disconnect.
7. Supply an authorized OpenAI organization admin API key and configured enforced monthly hard spending limit; compare the reported dollars, usage, dates and warning thresholds against the official dashboard.
8. Leave Anthropic, Z.AI, OpenCode subscription quota cards hidden until supported and documented quota data is retrievable; do not equate local token stats with quota remaining.
9. Confirm data minimization, opt-in activity monitoring, and secure storage of account credentials; inspect exported settings and diagnostic data.
10. Obtain a signed release only with a valid publisher certificate. A GitHub Actions build and unsigned installer are not equivalent to a trusted, code-signed shipping release.

## Download

Open [GitHub Actions — Windows installer](https://github.com/Hakkam/AetherIsland/actions/workflows/windows-release.yml) and choose the latest **successful** run for branch `AetherIsland`. In `Artifacts`, download `AetherIsland-Windows-x64`. Its contents are the installer and the self-contained portable ZIP.

Avoid using the older manual runtime-dependent package committed with the initial source.
