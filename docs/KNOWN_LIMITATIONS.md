# Known limitations — V2.1

## Windows-imposed

1. **Wi-Fi scanning needs Location permission.** Windows blocks WLAN network
   enumeration for apps without user-granted Location. The Network module
   detects this and offers the Settings page; radio on/off state still comes
   from `Windows.Devices.Radios` (no Location needed). Connecting to a
   secured network uses its **saved Windows profile**; first-time password
   entry stays in Windows (WlanConnect cannot submit passwords from a
   desktop app without a profile).
2. **Bluetooth pairing** stays in Windows Settings (pairing UI is OS-owned);
   paired devices + connection state are listed.
3. **Dark-mode toggle** writes the documented `Personalize` registry values
   and broadcasts `WM_SETTINGCHANGE` — the de-facto mechanism; a future OS
   could change it (failure falls back to opening Windows settings path in
   the balloon text).
4. **Camera mirror** depends on a camera + privacy consent; errors surface in
   the window instead of a blank pane.
5. **Screen-time sampling** counts the foreground process only while consent
   is on and the session is not idle (≥5 min). Elevated processes report no
   icon. No titles/URLs/keystrokes are ever read.

## Scope decisions

6. **AI quota module removed** (2026-11-08, user request). No AI provider
   cards, no API-key storage, no AI endpoints — the surface is simply gone.
7. **Calendar OAuth (Google/Microsoft) is not implemented.** Public ICS URLs
   and local .ics files work. The reference's "Connect Google / Outlook"
   link routes to this documented workflow.

## Fidelity deltas

8. Weather chips show daily UV max (not instantaneous UV) and no separate
   precipitation chip (hourly precip % appears in the hourly strip).
9. WLAN rows show signal % rather than 2.4/5 GHz band (band requires the
   BSS-list API, deliberately skipped to keep the scan light).
10. Icons are hand-drawn vectors (no Segoe MDL2 dependency), so glyph shapes
    differ slightly from the reference while matching roles and sizes.
11. Arabic RTL: language plumbing + Core dictionaries exist; full RTL layout
    QA is pending — treat Arabic as partial.

## Verification boundaries

12. Mouse-drag of the island, tray left/right clicks and Explorer drag-drop
    need a human pointer; they are implemented and structured for it but not
    exercised in this agent session (verified: hotkey toggle, geometry,
    rendering, persistence, installer, uninstall, startup key).
13. Working-set size on this machine (~350 MB idle) is dominated by the Intel
    GPU user-mode driver loaded in-process by WPF (~140 MB) plus the .NET/WPF
    runtime; the managed heap sits near zero at idle. CI runners report
    different absolute numbers for the same binary.
