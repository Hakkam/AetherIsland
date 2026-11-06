# Architecture

## Projects

- `src/AetherIsland.Portable` — WPF shell (net10.0-windows10.0.19041.0,
  PerMonitorV2). Procedural UI (no per-view XAML) built from a shared control
  kit; explicit island state machine; `--ui-selfcheck` and `--capture-views`
  acceptance modes.
- `src/AetherIsland.Windows` — Win32/WinRT interop: foreground tracker,
  WLAN, audio endpoints, system metrics, power helpers, shell icons, DPAPI
  credential store, native methods.
- `src/AetherIsland.Integrations` — Open-Meteo weather + geocoding, SMTC
  media watcher, ICS calendar, OpenAI Admin usage provider + aggregator.
- `src/AetherIsland.Core` — pure logic (day-progress + quota math), shared
  AI usage models/contracts, localization dictionaries.
- `tests/AetherIsland.Tests` — 27 unit tests: day math, quota math/alerts,
  ICS parsing.

## Shell design

`MainWindow` (partial classes per module) owns:
- **State machine** — `Collapsed ⇄ Expanded(module)`, `Pinned`, drag
  threshold distinguishes click vs move; no focus-loss auto-collapse; no
  fullscreen polling. Position persisted as the equivalent pill coordinates.
- **Navigation** — 13 modules rebuilt lazily on visit; other tabs keep no
  timers/subscription behind them (media subscription starts once, on first
  expand; focus timer ticks only while running).
- **Shared services** — `FocusService` (single source for pill + module),
  `Scheduler` (one 60 s timer for task reminders + wellbeing + day
  rollovers), `ActivityTracker` (opt-in sampling, 7-day rolling history),
  `Toggles` (radios/dark-mode/audio/awake/screenshot/lock with honest
  failures), `StartupManager` (self-healing Run key), `PortableData`
  (atomic JSON writes, bounded rotated log, DPAPI credentials).
- **Theming** — `Theme` tokens (dark/light/system + accent) with frozen
  brushes; theme change re-renders chrome + current module.

## Data flow principles

1. Idle cost stays near zero: event-driven updates; per-minute clock; no
   module polls while not visible.
2. Every persisted write is atomic (temp + move) and lock-guarded.
3. Native calls never fabricate success: failures either disable the control
   or surface text/balloon feedback.
4. The window renders vector-only iconography (no font glyph dependencies).

## Build & release pipeline

`tools/publish-portable-v2.ps1` → self-contained win-x64 publish +
`portable.flag` + ZIP + SHA-256.
`tools/package-installer.ps1` → same publish + Inno Setup compile
(`installer/AetherIsland.iss`, per-user, preserves `Data\`).
CI (`.github/workflows/windows-release.yml`) builds, tests, publishes,
runs the live WPF self-check, records idle resources, captures every module
view, compiles the installer and uploads ZIP + installer + captures as
artifacts.
