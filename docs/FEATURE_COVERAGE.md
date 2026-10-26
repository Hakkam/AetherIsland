# Feature coverage — 2026-10-26

Statuses distinguish source coverage from interactive acceptance. The Windows Debug and Release solutions compile with zero warnings and zero errors. A direct invocation of 27 existing test cases passed. VSTest could not connect to its testhost in this environment, and interactive module acceptance has not been completed.

| Feature | UI | Logic / integration | Persistence | Status |
|---|---|---|---|---|
| Floating island, navigation, tray, hotkeys | Present | WPF and Windows interop | Settings | Implemented |
| Dashboard and quick actions | Present | Windows actions / weather | Settings | Implemented |
| File shelf | Present | Explorer drag/drop and references | SQLite | Implemented |
| Tasks and notes | Present | Local CRUD | SQLite | Implemented |
| Focus timer | Present | Countdown / notifications | SQLite | Implemented |
| Media | Present | Windows media sessions | Session state | Implemented |
| Weather | Present | Open-Meteo | Settings / cache | Implemented |
| Calendar ICS | Present | Local ICS file or HTTPS feed | Settings | Implemented |
| Google / Microsoft calendar authorization | Absent | OAuth not implemented | Absent | Not Started |
| Productivity | Present | Foreground and idle sampling with consent | SQLite | Implemented |
| Network | Present | WLAN and Bluetooth enumeration | Optional | Implemented |
| System metrics | Present | Windows sampling | Optional | Implemented |
| Wellbeing | Present | Break / hydration controls | SQLite / settings | Implemented |
| AI monthly API spend limit | Present | OpenAI Admin limit and cost | DPAPI key / settings | Implemented |
| Anthropic / Z.AI subscription limit cards | Excluded | No compatible verified connector in this version | Existing settings retained | Unsupported |
| Local agent status | Present | Process and metadata detection with consent | Consent | Implemented |
| Camera mirror | Absent | Absent | Absent | Not Started |
| Navigation and dashboard layout editor | Present | Show/hide, order, live refresh | JSON | Implemented in source |
| First-run setup | Present | Appearance, behavior, privacy | Settings | In Progress |
| Installer / signed release | Source only | Framework-dependent manual ZIP and tested file-copy installer; Inno Setup source; unsigned | N/A | Installer compiler and self-contained packs unavailable |
| Arabic RTL / accessibility / multi-monitor | Partial | Window flow, selected display, accessible move commands | Settings | In Progress |

## Acceptance still required

1. Re-run the standard VSTest runner outside this restricted session; the 27 cases passed through a direct harness, while the runner failed to connect to testhost here.
2. Open each module, exercise commands, restart, and confirm persistence.
3. Validate drag/drop, DPI, multiple monitors, fullscreen, suspend/resume, and tray lifecycle.
4. With an authorized OpenAI organization admin key, compare monthly limit and cost with the official dashboard, including zero spend and no enforced cap.
5. Review Arabic RTL layout and compare the floating dashboard with the supplied reference.
6. Inspect the published framework-dependent folder and compile the Inno Setup installer. Inno Setup 6 and self-contained runtime packs are unavailable here.

## 2026-10-26 / Windows packaging follow-up

- Added GitHub Actions workflow to build and package on a real Windows runner.
- CI invokes .NET 10 restore/build and produces a self-contained win-x64 publish plus Inno Setup installation executable; the generated assets are attached to successful workflow runs.
- Added removable manual-install tooling that preserves local user data by default.
- These are source-level improvements until a successful workflow run and Windows interactive acceptance confirm the actual artifacts.
- Existing missing requirements (Google/Microsoft OAuth, camera mirror, hardware-dependent Windows acceptance) remain incomplete. Do not mark them verified.
