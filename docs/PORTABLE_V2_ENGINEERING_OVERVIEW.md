# Portable V2 — engineering roles, scope and acceptance overview

Date: 2026-10-26

## Skills and specialist disciplines

| Discipline | Competency applied | Primary responsibility |
| --- | --- | --- |
| .NET 10 / C# | async cancellation, nullable-safe DTO use, reliable lifetime | dependency and build correctness |
| Windows WPF / XAML | input routed events, HWND focus, animation FillBehavior | click, drag, expansion and navigation |
| UX / visual systems | design tokens, screenshot measuring, optical balance | fidelity to two images and video |
| Win32 integration | tray, ShellExecute, WLAN, SMTC, foreground process | functional actions rather than mock toggles |
| Portable Windows delivery | self-contained publish, x64 packaging, SHA-256 | installer-free portable ZIP |
| Data engineering | atomic JSON persistence, lifecycle flush, recovery | notes, tasks, shelf and opt-in screen time |
| Windows security | DPAPI CurrentUser and least-privilege API keys | protect admin credentials across local sessions |
| AI provider API integration | quota provenance, hard-limit semantics, billing scopes | OpenAI API only when demonstrably enforced |
| Performance engineering | on-demand work, lazy subscriptions, bounded sampling | avoid idle CPU, WMI and network work |
| Quality engineering | behavioral acceptance, regressions, DPI and standby cases | prove usability beyond successful compilation |
| Accessibility / localization | keyboard focus, labels, no inaccessible hidden actions | operability across Windows UI settings |
| Release engineering | clean artifact tree, reproducible workflows and hashes | versioned release and traceability |

No independent coding-agent spawning tool was available in the active work environment. Tasks were separated into source-level areas, not misrepresented as independently verified parallel-agent output.

## Three completed source workstreams

### Phase 1: Shell and portability (implemented in source)

- Independent `AetherIsland.Portable` app instead of the old `AetherIsland.App` shell.
- Removed automatic deactivate collapse and periodic fullscreen hiding.
- Compact click/drag shell, expanded reference-style rail, persistent position and a tray exit command.
- Local `./Data` root and atomic JSON settings, bounded error diagnostics and Windows-user-bound DPAPI credentials.
- No auto-start registry writes or installation.

### Phase 2: Reference interaction paths (implemented in source)

- Compact four-section dashboard; real Windows Settings/system launch actions.
- File drop shelf, editable notes, task completion, focus countdown.
- Weather from Open-Meteo after explicit location selection; visible error/empty states.
- WLAN status through native API, playback through Windows SMTC controls.
- Screen-time tracking optional and off by default, no keystrokes/window titles.
- No camera, calendar OAuth or unverified embellishments added to the new app.

### Phase 3: Truthful AI and repository cleanup (implemented in source)

- Only OpenAI organization API enforced hard monthly spend cap is displayed after API confirmation.
- No mock providers, estimates, manual caps presented as provider quotas, or cookie scraping.
- API key encrypted with DPAPI and no silently transferable secrets.
- Multi-file self-contained win-x64 ZIP packaging plus SHA-256 checksum.
- Removed previous installed-app shell, stale installer, checked-in platform-specific releases, unrelated helper scripts and unsupported provider source implementations from this branch.

## Hard stops before claiming production readiness

1. Successful Windows compile and test run of V2 solution and verified ZIP output.
2. Human interaction test on Windows showing opening, navigation, drag, close, tray exit and startup recovery.
3. Screenshot comparison for each supplied reference plus equivalent video interaction replay at matching display scale.
4. Performance measurements from a 10-minute idle capture (CPU average target <= 1%, warm private working set target <= 180 MiB; these are not yet measured facts).
5. Test extract/launch on a clean Windows x64 system without an installed .NET runtime; move the directory and verify persistent data.
6. Real authenticated OpenAI Admin account test for an enforced hard cap and cost reconciliation; otherwise no numeric card.
7. Check files, notes, tasks, Wi-Fi, media, screen-time opt-in and weather against their empty/error/data states.
8. 100%, 125% and 150% DPI, RTL rendering if enabled, second monitor, app foreground mode, explorer restart and sleep/resume.
9. Verify code signing status and Windows SmartScreen UX before recommending wider public distribution.

The absence of these proofs must be visible in every release overview. Source implementation is NOT equivalent to fully validated software, and cannot be advertised as 100% complete based on a passing CI workflow.
