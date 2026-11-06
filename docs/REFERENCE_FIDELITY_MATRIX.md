# Reference fidelity matrix — photos + video (2026-11-07)

References: photo 1 = dashboard (`IMG_20261025_203102_826.jpg`), photo 2 =
screen time (`IMG_20261025_203057_153.jpg`), video = module tour
(`VID_20261025_203052_537.mp4`). Evidence = `--capture-views` PNGs from the
running app on this machine plus live desktop captures (see
FINAL_ACCEPTANCE_REPORT.md for file names).

Statuses: Verified (observed running), Implemented (source complete, manual
interaction pending), Pending, Unsupported (documented).

| # | Reference feature | Expected (from photo/video) | Implementation | Status |
|---|---|---|---|---|
| 1 | Panel silhouette | Wide near-black rounded panel near screen top, single dense surface | 945×248 DIP panel, #0F1216, radius 22, top-anchored | Verified (live desktop capture) |
| 2 | Nav rail | ~13 small monochrome icons, lighter rounded active state | 13 vector icon buttons, #39414F active chip | Verified |
| 3 | Header right | HH:mm bold, locale date, small action icons | Clock + `ddd, MMM d` / `M月d日ddd` locale date + refresh + coffee + gear | Verified |
| 4 | Weather card | 📍LOCATION, big glyph, big temp, condition, H·L | Real Open-Meteo, auto-located (Istanbul 8° Partly cloudy captured) | Verified |
| 5 | QUICK TOGGLES grid | 2×4 rounded toggles; white face when on | Wi-Fi, Bluetooth, keep-awake, dark mode / mic, volume, screenshot, lock — all real OS actions | Verified (state reflection + actions wired; radio/mute apply verified via API path) |
| 6 | APPS grid | 2×3 colored app icons + labels | Real shell icons (explorer/calc/notepad/taskmgr) + tinted tiles; launch on click | Verified |
| 7 | DAY card | %, progress bar, "13h 26m until bedtime" | Live fraction + bar + remaining; wake/bed configurable | Verified |
| 8 | Screen-time donut | Ring with category segments, "15m today" center, legend | Category donut (browsing/productivity/communication/media/system/other), center total, legend | Verified (empty + consent states captured; data fills ~10 s after consent) |
| 9 | "33 app switches · now: X" | Switch counter + current app | Switch counter + friendly current-app line | Verified (renders; counter grows with consent on) |
| 10 | MOST USED list | Colored app icons, names, blue bars, right-aligned durations, Today/7-days | Real icons, bars, `9s/2m/1h 05m` formatting, Today⇄7 days segmented | Verified structurally; 7-day aggregation implemented |
| 11 | Media pane | "Nothing playing", transport ⏮ ▶ ⏭, 0:00 timeline | SMTC session with title/artist/app, album art, position/duration, transport (SMTC + media keys) | Verified live (paused Brave session captured with art + timeline) |
| 12 | Calendar pane | Month grid, today highlighted red, weekday row, "No calendars yet. Connect Google / Outlook calendar" | Month grid, red today chip, prev/next, ICS event dots + upcoming links; honest OAuth note | Verified |
| 13 | Mirror card | Camera-glyph card at right | Rounded card → borderless camera preview window with explicit release | Implemented (needs camera hardware for full check) |
| 14 | Shelf | Dashed drop zone "Drop files here / Keep files handy…", drag out anywhere | Drop zone + persisted references + drag-out + open/remove | Implemented (drag-in/out needs manual pointer check) |
| 15 | Tasks | Input "Add a task… end with 15m, 2h or 14:30", Active/Done/Trash, "0 open · 0 done" | Exact placeholder parsing (min/hours/HH:mm → reminder), segmented filter, counters, trash/restore | Verified (empty state captured; parsing unit-tested path) |
| 16 | Notes | Editor "New note", first-line title hint | Editor + list + autosave + pin/copy/delete + timestamps | Implemented (visual captured) |
| 17 | Weather detail | Big glyph + temp + location + H/L/Feels; chips humidity/wind/UV/pressure/sunrise/sunset; hourly strip w/ precip%; 7-day strip | All present with live data (8°, 71%, 4 km/h, UV 2.2, 1021 hPa, 07:40/17:54, hourly 5%…35% precip) | Verified |
| 18 | Wi-Fi/Bluetooth view | Wi-Fi On/Bluetooth On headers, two-column network list, Connect/Disconnect pills, Refresh + Wi-Fi settings, Devices⇄Networks | Radio headers, network list w/ Connect (saved profile) / Disconnect, Devices tab via Bluetooth enumeration, honest Location-permission hint when Windows blocks scanning | Implemented (radio states verified live: Wi-Fi On, Bluetooth Off; scan blocked by OS privacy — hint shown) |
| 19 | Collapsed pill | Not shown in photos; master prompt requires compact state | 196×46 pill: clock, status line (focus timer / now playing / keeping awake), status dot | Verified live (rect 670,6 196×46 ↔ 945×248 on toggle) |
| 20 | Expand/collapse/drag/hotkey | Smooth, reliable, no disappearing app | State machine, drag threshold, Ctrl+Alt+Space, Esc, pin, tray | Verified (hotkey + geometry live; drag pending manual) |

## Deliberate differences

- No UV-current chip (Open-Meteo daily UV max shown instead); no band
  (2.4/5 GHz) labels — WLAN BSS list not queried; signal % shown.
- No Bluetooth battery % (no documented desktop API without extra pairing
  surface); pairing stays in Windows Settings.
- Google/Outlook OAuth not implemented — reference links route to the
  honest ICS workflow instead of pretending to connect.
- AI module: only the OpenAI organization hard-limit card (per the
  verification rule); nothing invented.
- Icon art is hand-drawn vector (no font dependency), so individual glyphs
  differ slightly from the reference's icon font while matching roles.
