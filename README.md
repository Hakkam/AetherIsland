# Aether Island — Portable V2

Lightweight, reference-led floating Windows utility. **Portable-first**: unzip and run, no administrator permission and no separate .NET Desktop runtime required.

## Current structure

- `src/AetherIsland.Portable` — rebuilt WPF shell, fast navigation, app features, local persistence
- `src/AetherIsland.Core` — shared AI usage contracts and domain logic
- `src/AetherIsland.Windows` — Windows native APIs, SMTC helpers and credential encryption
- `src/AetherIsland.Integrations` — OpenAI Admin reporting and live weather/media/Wi-Fi adapters
- `tests/` — foundational logic tests; Windows visual and lifecycle acceptance is separate
- `tools/publish-portable-v2.ps1` — self-contained Windows x64 ZIP build and SHA-256 checksum

The obsolete installer, precompiled release files, unused provider integrations and old WPF shell were removed **from this rebuild branch**. Historical versions are still available in Git history and the `AetherIsland` baseline.

## Launch

1. Download the **AetherIsland-Portable-V2-win-x64** build artifact from the latest successful Windows build.
2. Extract `AetherIsland-Portable-V2-win-x64.zip` completely to a folder with write permissions.
3. Open `AetherIsland.exe`. Click the pill to expand; drag to move; choose a module from the icon rail. Right-click the notification-area icon for Exit / Settings / Reset position.
4. User data remains in `Data/` alongside the executable. To uninstall, exit and delete the extracted directory. Never delete `Data/` before exporting notes and tasks you wish to retain.

### Main modules

Dashboard (real OS shortcuts and day progress), live weather, Wi-Fi network status, Windows SMTC media, file shelf, tasks, notes, focus timer, opt-in coarse screen time and **verified OpenAI API monthly organization hard spend limit**.

There is **no support** for ChatGPT/Claude/Codex subscription usage percentage, OpenCode, Z.ai or any provider without a verified and documented remaining allowance. OpenAI API requires an organization Admin key and an enforcing monthly hard spend limit; otherwise the quota card remains absent. An ordinary ChatGPT subscription has no quota access through this API.

Credentials are protected by Windows DPAPI and bound to the current Windows account. Moving the portable directory to a different account requires re-entering credentials.

## Performance

Designed for no idle background integration polling. The UI clock updates once per minute, network and weather fetch only on demand, activity sampling requires explicit consent and runs approximately every 10 seconds, media session is initialized lazily, and focused countdown updates only while running.

CPU/RAM thresholds are **target budgets, not measured results** until captured on real hardware.

## Documentation

- [Three-stage reconstruction plan](docs/PORTABLE_V2_THREE_PHASE_REBUILD.md)
- [Portable V2 usage and limitations](docs/PORTABLE_V2_README.md)
- [Engineering skill ownership and final acceptance](docs/PORTABLE_V2_ENGINEERING_OVERVIEW.md)

## Building

On Windows 10/11 with .NET SDK 10:

```powershell
./tools/publish-portable-v2.ps1
```

Output: `release/portable-v2/AetherIsland-Portable-V2-win-x64.zip` and matching SHA-256 file.

**Release caveat:** The application code and build pipeline are not equivalent to a human-verified interactive build. Tests, compiled artifacts and on-device acceptance are reported separately; unsigned binaries may prompt Windows SmartScreen.
