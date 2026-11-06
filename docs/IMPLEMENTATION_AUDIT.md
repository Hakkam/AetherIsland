# Implementation audit — AetherIsland Portable V2.1 (2026-11-07)

Scope: full audit + rebuild of `src/AetherIsland.Portable` against the two
reference photos (`IMG_20261025_203057_153.jpg`, `IMG_20261025_203102_826.jpg`)
and the reference video (`VID_20261025_203052_537.mp4`), plus the master
completion prompt. Baseline audited: branch `AetherIsland`, commit `7854052`.

## Starting state (baseline 7854052)

- Single-project WPF shell (~1,400 lines) with 10 modules, procedural UI.
- Verified clean build, 27 unit tests, a `--ui-selfcheck` CI smoke.
- Honest but sparse UI: text buttons, no reference quick-toggle grid, no
  colored app icons, no calendar, no camera mirror, minimal settings.
- Support layers compiled but unwired: `AudioEndpoints` (mute),
  `SystemMetricsSampler`, `PowerHelper` (keep-awake/lock/media keys),
  `ShellIcons`, `IcsCalendarProvider`, `Loc`.
- Known risks from prior audit (kept in mind, fixed where in scope):
  swallowed exceptions in native layers, non-atomic DPAPI store writes.

## What this pass changed

### Shell
- 13-module navigation (home, dashboard, shelf, tasks, notes, focus, media,
  wellbeing, screen, weather, network, ai, utilities) + settings via header
  gear, matching the reference icon rail.
- Reference header: locale clock + date, refresh, keep-awake coffee (orange
  when active), settings gear.
- Pill/panel state machine preserved (click=expand, drag-threshold=move,
  Esc collapse, pin, no focus-loss auto-collapse, no fullscreen polling).
- Global hotkey Ctrl+Alt+Space (RegisterHotKey), tray menu with Open,
  Refresh, Settings, **Launch at Windows startup** toggle, Reset position,
  Exit. Balloon notifications via the tray icon.
- Theme system: dark/light/system + five accent colors; light theme derived,
  dark theme matches the reference palette; `--capture-views=<dir>` mode
  renders every module to PNG for visual acceptance.

### Real integrations (no simulated toggles)
- Wi-Fi/Bluetooth radio on/off: `Windows.Devices.Radios` (documented API),
  state reflected asynchronously; unavailable radios disable honestly.
- Dark mode: `AppsUseLightTheme`/`SystemUsesLightTheme` + WM_SETTINGCHANGE
  broadcast; app theme follows.
- Microphone/speaker mute: `IAudioEndpointVolume` via existing
  `AudioEndpoints`; state read back from the OS after applying.
- Keep awake: `SetThreadExecutionState` refreshed by a dedicated thread.
- Screenshot: virtual-screen capture → clipboard + `Data/screenshots`.
- Lock: `LockWorkStation`. Media keys: `keybd_event`.
- Weather: Open-Meteo with 30-min per-location cache; IP auto-location
  (ipapi.co → ipinfo.io fallback) with honest failure.
- Screen time: opt-in, ~10 s foreground sampling, exe names only, app-switch
  counting, 7-day rolling history, category donut, per-app icons.
- Media: SMTC metadata/timeline/album art, transport controls.
- Calendar: ICS sources (URL/file) with month grid + event days; Google/
  Outlook OAuth deliberately unsupported and labeled as such.
- Camera mirror: `MediaCapture` preview (~15 fps stills path), explicit
  open/close and disposal, truthful error states.

### Performance
- Idle: clock timer (1/min), scheduler tick (1/min), focus timer only while
  running, activity sampling only when consented (~10 s), media subscription
  event-driven after first expand. No polling loops, no fullscreen polling.
- Measured on this machine (iGPU laptop-class): idle CPU 0.00% over a 60 s
  window (collapsed and expanded); GC heap ≈ 0 idle; see FINAL_ACCEPTANCE_REPORT
  for the working-set breakdown (dominated by the Intel GPU user-mode driver
  loaded in-process by WPF's renderer, plus .NET/WPF/WinRT runtime).

## Outstanding gaps (honest)

- Interactive drag, tray-click and drop-verification were implemented but not
  exercised by hand in this session (agent has no pointer); hotkey expand/
  collapse and live rendering were verified programmatically.
- WLAN network list requires Windows Location permission; the app detects the
  empty-scan case and offers the Settings page instead of pretending.
- Bluetooth pairing stays in Windows Settings (documented limit); devices and
  connection state are shown.
- Camera mirror needs a camera; error states are shown otherwise.
- Arabic RTL: language setting + framework exist (Core `Loc`); full string
  coverage and RTL layout QA are not complete — documented as partial.
- AI quota: OpenAI organization hard-limit only, by design (see
  AI_PROVIDER_CAPABILITIES.md).
