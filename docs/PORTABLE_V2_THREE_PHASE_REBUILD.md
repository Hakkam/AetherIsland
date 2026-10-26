# Aether Island — Portable V2: Three-phase reconstruction plan

Date: 2026-10-26
Repository: Hakkam/AetherIsland
Baseline audited: branch `AetherIsland`, commit `b60a7e83751e3813d85e5609af2d6bd33867a698`.
Status: **planning and source audit, not a completed rebuild**. Existing install/build smoke tests did not validate interactions or reference fidelity.

## Product definition and precedence

**Deliverable:** a fast, self-contained Windows 10/11 x64 **portable ZIP** that runs by extracting and opening AetherIsland.exe, does not require .NET Desktop Runtime or administrator permission, and can be removed without an uninstaller.

**Reference authority:** the two user photos and ~8-second reference video are the visual and interactive source of truth. Only directly evidenced interactions are in required scope; ambiguous features must be documented as such rather than invented. The previous 27-part master prompt is secondary where it conflicts with the new narrowed requirement: visual faithfulness, lightweight behavior, and verified AI remaining quotas only.

Observed visual layout:
- Wide, contiguous near-black rounded surface anchored near the top of the monitor; not four visually separated large cards.
- Persistent narrow navigation of small monochrome icons across the top, with a compact date/time and small actions at the right.
- Home/dashboard: weather, quick toggles, application shortcuts, and day-progress in one horizontal composition.
- Screen time: compact ring breakdown, categories and top-app horizontal usage bars.
- Video navigation includes media control, a file drop shelf with add/remove/share affordances, notes editor, weather forecast and Wi-Fi/Bluetooth view.
- Expand/collapse transitions, navigation changes, active button states, meaningful loading/empty/error states.
- Do not copy reference screenshots as textures; implement controls.

## Audit findings and suspected causes

**Critical — disappearing panel**:
- `src/AetherIsland.App/Windows/IslandWindow.xaml`: `ShowActivated="False"` and layered `AllowsTransparency="True"`, combined with `IslandWindow.xaml.cs` `Deactivated` → `Collapse()` when collapse-on-deactivate is enabled. This is a plausible focus-loss/reentrancy failure. Actual cause needs foreground/focus logs on a real Windows desktop.
- `IslandWindow.xaml.cs` polls foreground-window geometry every 2 s to detect fullscreen and sets `Visibility.Hidden` for an apparent fullscreen match. Maximized/borderless applications and Desktop hosts require robust filtering and tests.
- `App.xaml.cs` registers a global `DispatcherUnhandledException` handler that always sets `Handled=true`, writes only to Trace, and gives neither structured logs nor a repair/diagnostic UI. A failed view may appear blank rather than crashing visibly.
- The expanded layout is switched by collapsing one entire visual tree and revealing another before width/height transitions. Consolidate into an explicit interaction state machine and verify every transition.

**Major — visually different reference**:
- `DashboardView.xaml` uses a 4-column/2-row `Grid` with independent `WidgetCard` borders; `DashboardView.xaml.cs` switches to one row when wide. It is structurally different from the photographed single dense dark panel.
- Default expanded size is 860x430 (`AppSettings.Appearance`), proportionally much taller than the supplied wide/short reference surface. Navigation, spacing, app glyphs, weather information hierarchy, and screen-time visualization need reconstruction against captured reference crops.

**Major — not truly portable**:
- The existing ZIP publishes self-contained binaries, but `SettingsService`, `Db`, `DpapiCredentialStore`, `OpenMeteoService` and `LauncherStore` default to `%LOCALAPPDATA%\AetherIsland`. This is installation-free, not portable user-data storage.
- DPAPI `CurrentUser` encryption is the right default protection but encrypted keys are not transferable to different Windows users: the app must request reauthorization after relocation, not downgrade to plaintext.

**Major — missing end-to-end coverage**:
- 27 prior tests cover day progress, quota math/alerts and ICS parser, not WPF shell open/close, drag, navigation, visual comparison, lifecycle, portability, network and memory.
- Build, packaging, silent install and uninstall tests are useful but did not exercise a live interactive window.

**Major — unnecessary background costs**:
- `App.StartServices` initializes media, activity tracking, health timer, weather, AI refresh at startup.
- `ScreenTimeService` samples foreground every 1 second when opted in; `IslandWindow` polls fullscreen every 2 seconds.
- `SystemViewModel.Tick()` calls `SystemMetricsSampler.Sample()` every second on the dispatcher. That includes WMI temperature probing and network counters; every 3 seconds it enumerates processes and every 10 seconds it loads drives. Move these operations to a cancellable background worker and reduce frequency.
- `NetworkViewModel` refreshes every 20 seconds while active and enumerates devices; `MediaViewModel` refreshes every 500 ms while active. Apply visibility/consent-aware sampling and limit overlapping requests.
- Using `AllowsTransparency=True` with a broad per-window `DropShadowEffect` risks poor rendering performance; prototype DWM-supported opaque rounded windows and measure both approaches.

**AI truthfulness**:
- `AiUsageService.BuildProvider()` only enables OpenAI. `UsageAggregator` excludes all cards lacking an official verified positive limit. Its current empty-state UX should give a clear reason rather than imply that something is connected.
- OpenAI Admin APIs document GET `/v1/organization/spend_limit` and organization usage/cost APIs. A configured **enforced monthly API hard spend limit** can be an eligible quota **only** after live account response and monthly cost reconciliation. This is not ChatGPT subscription usage or an OpenAI-approved monthly usage-tier cap.
- Anthropic Admin usage/cost reports alone do not establish a particular subscription's remaining allowance. Z.ai/ZCode may show account quotas in its own client, but that does not prove a supported third-party API connector. OpenCode local session tokens are not an upstream subscription quota.
- Exclude every provider whose actual limit and used amount cannot be obtained from authorized, documented, reconcilable source data. No mock data, invented progress, manual budgets portrayed as limits, cookie scraping, or browser-session token theft.

## Phase 1 — Replace shell and reproduce the reference, portable-first

Work:
1. Freeze prior code as legacy reference. Rebuild a minimal `App` + `IslandHost` with **explicit states** `Collapsed`, `Expanded(module)`, `Pinned`, `Dragging`, and `FullscreenSuspended`; only named events can change state. No automatic collapse while interacting with a child control or opening the tray.
2. Implement focus-safe WPF topmost behavior, click vs drag threshold, multi-monitor work area and 100/125/150/200% DPI; handle Explorer restarts. Prefer GPU-accelerated opaque/DWM window surfaces to whole-window layered transparency. Respect Reduced Motion.
3. Rebuild the top rail and dashboard pixel by pixel from visual measurements and the original images/video. Accurate iconography, spacing, near-black palette, active icon backgrounds, four horizontal widgets, and a responsive fallback with the same information hierarchy.
4. Build the portable folder: `AetherIsland.exe`, dependencies, `Data/settings.json`, `Data/app.db`, `Data/cache/`, `Logs/` (created only as needed), and `portable.flag`. No admin, registry entries, scheduled tasks, autostart, or Windows installation. Explicitly warn if storage is read-only.
5. Safe single-instance handling per portable data root; meaningful diagnostics: rolling bounded local log, exception classification, recovery action if a view fails. Never swallow exceptions and pretend an action succeeded. Default telemetry OFF.
6. Start only clock + UI shell. Disable heavyweight modules/services while Stage 1 interactive shell is verified.

Acceptance gate A:
- Clicking pill expands reliably; moving it with mouse does not open it; clicking navigation never hides app; Esc/collapse, pin, tray Exit, fullscreen, monitor changes work.
- Reference side-by-side screenshots for collapsed/expanded home at 100% and 150% scaling, including active navigation, no misplaced elements; validate with the actual Windows desktop.
- Move folder and run with no .NET Runtime/installer, reboot Windows, relaunch from the folder; settings remain inside `Data/`; deletion removes program without orphaned autostart.
- Idle resource benchmark baseline measured before and after: target mean CPU <=1% over a 10-minute idle period on the test machine, warm working set <=180 MiB, negligible idle GPU and no repeated disk/network requests. Track 95th percentile interaction latency and animation frame times; investigate any regressions.
- A self-contained `AetherIsland-Portable-win-x64.zip` generated by CI, with SHA-256, smoke-tested on a real Windows desktop (CI compilation alone is insufficient).

## Phase 2 — Reference functionality only, no decorative placeholders

Implement in reference-navigation order, keeping a single reusable header and module content container:
- File shelf: genuine drag-in and drag-out of actual Windows file paths, add/remove and clear action, safe reference persistence (no unintended file copying), intentional share/Explorer integration.
- Notes and tasks: real create/edit/delete/complete/reorder where evidenced, instant visible confirmation, debounced persistence and restart recovery.
- System quick actions: show actual OS state. A toggle must really perform its action, or be labeled clearly as opening Windows Settings (never a fake on/off switch).
- Media: actual Windows media-session metadata and play/pause/next/previous, graceful no-session state.
- Weather, forecast, date and day progress: correct live data, explicit location setup, caching with timestamps and offline mode; no fabricated Indonesia temperature or default weather values.
- Wi-Fi/Bluetooth: real discovery and connectivity statuses; use OS dialogs for secured Wi-Fi connection as needed and distinguish supported from unsupported operations.
- Screen-time view: ring, category breakdown and most-used apps matching photo, **opt-in** tracking, idle exclusions, low-frequency batched SQLite writes and clear pause/delete controls.
- Other icons/modules: implement only what was actually shown/operated in the reference video; remove unused camera/OAuth/system dashboards rather than allowing empty scaffold screens. Preserve legacy implementations separately until accepted.

Acceptance gate B:
- Every visible icon has a defined action and correct empty/loading/error/success states; every action is manually demonstrated on Windows, not only unit tested.
- Modules retain data after close/reopen and portable folder relocation.
- Visual comparisons captured against each screenshot and relevant video frame; responsive geometry, non-Latin/localized text and glyph rendering verified.
- Open/close and tab switching fast; no network calls when an unrelated tab is opened; sampling and subscriptions disposed on tab close.
- Resource budgets from A remain met with normal feature use and with AI disabled.

## Phase 3 — Verified AI limits, reliability, performance and portable release

Work:
1. Introduce `IQuotaProvider` with `ReadVerifiedQuotaAsync` returning provider, account scope, dimension, used, hard limit, reset timestamp and evidence provenance. A provider is registered only after its scope/endpoint/response are verified.
2. Enable only OpenAI organization API enforced hard spend cap after official admin permission and real test-account reconciliation. `Used` from official cost endpoint; `Limit` from documented spend-limit endpoint. Handle pagination, currency, clock boundaries, 401/403, 429 and unavailable limits. Do not present project limits as organization limits or subscription limits.
3. Anthropic/Z.ai/OpenCode/Codex/Claude Code quota cards **disabled/absent** until documented, supported, authorized and reconciled endpoints are proven. No mock providers. If zero providers pass, show a brief honest empty state, not invented percentages.
4. API key encryption via Windows DPAPI or Credential Manager; keys never written in plaintext to portable `Data/`. On moving to a different Windows account, require re-entry instead of silently falling back.
5. Cache with minimum 15-minute AI refresh and explicit manual refresh, backoff and timeouts; no polling if not configured. Alerts deduplicated; no secrets in logs.
6. Soak tests (30–60 minutes), stress-expand/navigation, screen sleep/wake, disconnected/offline mode, changing primary monitor and DPIs; profile CPU, memory, UI responsiveness, background disk/network and WPF rendering.
7. Publish only portable self-contained x64 ZIP with manifest/version/checksum, release notes and an interactive manual acceptance report. Inno Setup installer is a **later, separate deliverable**.

Acceptance gate C:
- No feature presented as working unless verified on Windows with reproducible steps.
- No false AI quota or provider cards; no authentication secrets in exports or screenshots.
- Resource performance targets and no idle polling/regressions verified with captured baseline.
- Release ZIP works from a fresh extracted directory and on a machine without a separately installed .NET Desktop Runtime; app state and credentials semantics are documented.
- Full traceability table: each reference frame/visible action → UI component → integration → acceptance evidence → status.

## Execution discipline

1. **Do not claim “complete” because CI compiled or 27 unit tests passed.** CI builds packages; desktop UI/interaction and hardware acceptance must be checked separately.
2. Complete gate A before enabling Phase 2 features; complete gate B before Phase 3 AI.
3. Do not implement speculative features merely because the old master prompt mentioned them. Request-specific narrowed scope has priority: the video, two reference photos, then verified AI limits.
4. Keep the last known baseline accessible in Git history and mark source that can be reused with a short rationale. Avoid transplanting problematic window lifecycle and heavy synchronous timers.
5. Progress report at each gate: screenshots, observed behavior, actual CPU/RAM measurements, bugs and fixes, portable artifact SHA-256, and unresolved blockers.
6. A failed or unsupported module must be hidden or expose an accurate disabled/error state; it must never simulate success.

## Reuse / discard boundary

- **Reuse only after verification:** arithmetic/date calculators, SQLite repository access patterns (with new injected portable path and schema migration), Open-Meteo JSON parsing/cache protocol, Windows media-session API adapter, WLAN enumeration, and credential encryption mechanism. Retain code behind interfaces and add tests before integrating into the new UI.
- **Rewrite rather than patch:** `IslandWindow` activation/fullscreen logic, global exception swallowing, dashboard grid markup/style system, nav/header composition, view lifecycle/timer orchestration and legacy coupled static service wiring.
- **Leave out until demonstrated by the references:** camera mirror, Google/Microsoft calendar OAuth, decorative system telemetry and ancillary modules that do not have an observable interaction in the supplied video. Existing code may remain archived but must not run in the portable product.
- **Repository hygiene:** remove tracked legacy `release/win-x64` binaries and dated source archives from the rebuild source tree, preserve provenance via Git history, and publish versioned runtime-specific ZIPs as CI artifacts/releases. Do not bundle Linux/macOS SQLite assets into a Windows-only portable package when publish trimming can safely exclude them.
- **Portable documentation:** distinguish an executable that runs without installing from fully portable *data*. The new data root must be explicit and testable; avoid silently using `%LOCALAPPDATA%` unless the user explicitly selects installed-mode storage.
