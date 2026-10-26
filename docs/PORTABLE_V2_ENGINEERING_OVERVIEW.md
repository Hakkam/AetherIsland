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

## Verified Windows build and Portable ZIP — 2026-10-26

Latest successful post-phase workflow: [Windows Portable V2 run 40317967047](https://github.com/Hakkam/AetherIsland/actions/runs/40317967047), code revision `b5202c56574433d7ebc689362c5e34101293214c`.

- `dotnet build AetherIsland.slnx -c Release`: **0 errors, 0 warnings**.
- Existing shared-domain tests: **27/27 pass**; these are not WPF mouse/visual tests.
- `dotnet publish` self-contained win-x64: success; ZIP and SHA-256 generated and verified; ZIP contains no personal `Data/` folder.
- Windows hosted-runner **process launch**: app did not exit on startup or during the 15-second idle sample, and created its `Data/` directory next to the portable EXE.
- Hosted-runner sample after warmup: mean CPU **0%** over 15 seconds (rounded by PowerShell); working set **91.1 MiB**. These measurements are **not** 10-minute laptop benchmarks and are insufficient to guarantee user-device performance.
- GitHub Actions artifact name: `AetherIsland-Portable-V2-win-x64`, artifact ID `12648080704`. Contains `AetherIsland-Portable-V2-win-x64.zip` and `AetherIsland-Portable-V2-win-x64.zip.sha256`.
- SHA-256 of the **distribution ZIP**: `0f86db4434a4a3cef082d91aa0f026145b89e1ba6b5a708829e54c40d1158051`. The GitHub Actions **artifact wrapper ZIP** has a different digest and must not be confused with the distribution ZIP.
- Artifact expires on 2026-11-25 (unless rerun or published as a separate release). Application binaries are unsigned.

**Acceptance still open:** compare actual GUI with reference photographs/video using a Windows desktop, multi-monitor/DPI tests, screen time and media exercises, AI Admin account tests with an actual enforced spend limit, long-duration resource profiling, and signed release distribution. Source changes were implemented across all three workstreams; this does **not** establish that all user-facing acceptance gates have passed.
