# Feature coverage — 2026-11-07 (V2.1)

Legend: **Verified** = exercised on real Windows here (this machine).
**Implemented** = source complete + captured visually, manual interaction
pending. UI / Logic / Persistence / Errors columns summarize implementation.

| Module | UI | Logic / integration | Persistence | Errors & empty states | Status |
|---|---|---|---|---|---|
| Collapsed pill | Clock + status + dot | State machine, drag threshold | Position in settings.json | — | Verified (live toggle) |
| Expand/collapse/pin/Esc/hotkey | Panel + nav rail | RegisterHotKey, animated snap | Pinned, position | — | Verified |
| Tray | Menu + balloon | Open/Refresh/Settings/Startup/Reset/Exit | Startup Run key | — | Verified (menu built; click pending manual) |
| Home | Overview cards | Day math, next task, quick actions | — | — | Verified |
| Dashboard | 4-column reference layout | Weather fetch, real toggles, app launch, day math | Weather loc, wake/bed | Honest placeholders | Verified (live) |
| Quick toggles | 2×4 rounded | Radios, dark mode, mute, keep-awake, screenshot, lock | — | Disabled/honest failure | Verified (API paths) |
| Screen time | Donut + legend + most-used | 10 s foreground sampling, switches, categories | screen-time.json (7 days) | Consent gate, empty states | Verified |
| Media | Art + timeline + transport | SMTC watcher + media keys | — | "Nothing playing" | Verified (live session) |
| Calendar | Month grid + events | ICS parser (URL/file) | CalendarSources in settings | "No calendars yet" | Verified |
| Mirror | Card → preview window | MediaCapture ~15 fps | — | Access/hardware errors | Implemented (hardware-dependent) |
| Shelf | Drop zone + rows | Drag in/out, open, remove | shelf.json | Empty state | Implemented (manual drag pending) |
| Tasks | Input + segmented | Reminder suffix parsing, trash/restore | tasks.json | Empty/filter states | Verified |
| Notes | Editor + list | Autosave debounce, pin/copy/delete | notes.json | Empty state | Implemented (visual captured) |
| Focus | Modes + clock | DispatcherTimer while running | Session counters | — | Implemented (visual captured) |
| Wellbeing | Cups + settings | Eye/stand reminders via scheduler | Settings | — | Implemented |
| Weather | Detail + editor | Open-Meteo + geocoding + IP locate | Location + unit + cache | Offline/cached labels | Verified (live data) |
| Network | Radios + lists | Radio state, WLAN scan, BT devices | — | Location-permission hint | Implemented (states verified; scan OS-blocked here) |
| AI quota | Removed | Removed from the product by user request (2026-11-08) | — | — | Removed |
| Utilities | Action rows | Screenshot/lock/tm/explorer/settings/clipboard | — | Balloon feedback | Implemented (visual captured) |
| Settings | Full page | Theme/accent/schedule/unit/startup/hotkey/privacy/calendars | settings.json | — | Verified (startup key written live) |
| Onboarding | In-panel first-run (location auto-detect, consent gates) | — | — | — | Implemented (auto-locate verified) |

Not implemented (documented, deliberate): Google/Microsoft calendar OAuth,
ChatGPT/Claude/Codex/Z.ai/OpenCode quota cards, per-app volume mixer,
WLAN band labels, Bluetooth battery %, camera-on-hardware verification.

## Acceptance scenarios run on this machine

1. Release build + publish: 0 warnings, 0 errors.
2. `dotnet test`: 27/27 passed (real VSTest run).
3. `--ui-selfcheck`: passed for the debug build **and** the installed exe.
4. `--capture-views`: 15 PNGs of every module from the running app.
5. Live desktop: expanded panel captured over real desktop; Ctrl+Alt+Space
   toggle verified via window geometry (196×46 ↔ 945×248).
6. Live data: weather auto-located (Istanbul) + hourly forecast; SMTC media
   session (Brave, paused, album art, 9:35/42:20); radio states.
7. Installer: silent install → files + Start Menu + uninstall entry →
   installed-exe self-check → silent uninstall (files gone, `Data/` kept,
   entry removed) → reinstall + launch (running now, startup key registered).
8. Performance: 0.00% idle CPU over 60 s (collapsed + expanded); GC heap ~0;
   working set dominated by in-process Intel GPU driver — measured, see
   FINAL_ACCEPTANCE_REPORT.
