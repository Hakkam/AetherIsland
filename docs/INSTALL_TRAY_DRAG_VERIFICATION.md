# Installation, tray icon, dragging and transitions — 26 October 2026

## What changed on branch `AetherIsland`

- **App icon:** Added a multi-size `Assets/AetherIsland.ico` and embedded it as the application icon. WPF Settings/Onboarding use the same resource; the notification-area icon prefers the EXE's embedded icon. Fixed fallback icon handle cleanup.
- **Tray right-click:** Open, Pin/Unpin, Reset position, Refresh AI usage, Check for updates, Settings, and Exit. Menu shows glyphs and refreshes labels according to English, Arabic or Turkish at each opening.
- **Explorer restart:** The tray icon responds to the *registered* TaskbarCreated window message and re-registers itself.
- **Keyboard shortcut:** The configured toggle hotkey now actually calls the island's Toggle action.
- **Position and motion:** Drag the compact island after moving the mouse past the Windows drag threshold; click without dragging to expand. In expanded mode drag the `⠿` grip or blank navigation rail, not action buttons. Save monitor, horizontal center offset and top offset in the user's settings. Reset position using the tray menu.
- **Animations:** Width, height, Left and Top use a 210 ms cubic easing transition. Dependency-property animations now stop at the target so they do not keep overriding manual dragging. Reduced-motion setting skips animation.
- **Updates:** The tray item checks the repository's latest **published GitHub Release** on explicit request, compares semantic versions, and asks before opening the official release page. It does **not** install anything or treat expiring Actions artifacts as stable published updates.
- **Inno Setup:** Per-user install and uninstaller, an application mutex preventing unsafe replacement while running, the same icon for Setup and Windows shell, start-menu/optional desktop shortcuts, and removal of this installation's own startup Run entry on uninstall.
- **Manual installers:** Installation refuses to overwrite a running application. Uninstall refuses paths without its installation marker and removes its own startup Run entry, but preserves user data by default.

## Automated Windows acceptance

Windows Actions run [40277134009](https://github.com/Hakkam/AetherIsland/actions/runs/40277134009) confirmed:

- .NET 10 Release compilation without errors or warnings.
- 27 passing unit tests.
- Successful Inno Setup EXE packaging and self-contained portable ZIP upload.
- A real unattended install, existence check for executable and uninstaller, and unattended uninstall on a Windows runner.
- Confirmed removal of the installed executable and matching HKCU autostart entry.
- Confirmed retention of a user-data marker under `%LOCALAPPDATA%\AetherIsland`.
- Successful manual installation/uninstallation from the portable folder with data preservation.

This is **installer/uninstaller verification**, not interactive visual acceptance.

## Manual UI acceptance remaining

Verify on your actual Windows 10/11 device:

1. In the taskbar notification area (possibly under the **^** overflow), right-click the island icon. Verify each menu label/icon and that **Exit** fully terminates the process.
2. Click the collapsed bar: it expands. Drag the collapsed bar: it moves without opening. Restart and verify the chosen position.
3. From the expanded bar, drag by `⠿` or empty top-rail space; pin, settings and navigation actions must still click normally.
4. Change language to Arabic and Turkish. Right-click again and verify text direction, labels, and icon alignment.
5. Try 100%, 125%, 150% Windows scaling and a secondary monitor. No off-screen positioning should occur; reset must return to the selected display's top-center.
6. Expand/collapse multiple times, test global toggle shortcut, reduced motion, fullscreen app, and Explorer restart.
7. Check **Check for updates**: it must identify a newer published Release when one exists, otherwise report no release or current version. It must never download or execute an unsigned binary without user approval.
8. Check Windows Settings → Installed apps → Uninstall, and confirm the app is removed but local notes/settings remain. On a new install check Start menu icon and desktop shortcut when selected.

**Security:** Do not deliberately bypass SmartScreen on unverified files. The generated installer is unsigned until a publisher signing certificate is configured.
