# Aether Island — module authoring contract (for view/VM authors)

This file defines the exact conventions module ViewModels and Views must follow.
Read the referenced files before writing code. Do NOT edit localization JSON files,
csproj files, or anything outside your assigned file list.

## Compile facts
- WPF, .NET 10 (`net10.0-windows10.0.19041.0`), nullable enabled, ImplicitUsings on.
- CommunityToolkit.Mvvm 8.4 is referenced: use `[ObservableProperty]`, `[RelayCommand]`
  on `partial` classes deriving `ObservableObject` (namespace `CommunityToolkit.Mvvm.ComponentModel`).
- XAML pages are auto-included by glob. NO builds — the integration pass compiles everything.

## Namespaces
- ViewModels: `AetherIsland.App.ViewModels`
- Views: `AetherIsland.App.Views` — root `<UserControl x:Class="AetherIsland.App.Views.XxxView" ...>`
- Services: `AetherIsland.App.Services` (static `AppServices`), `AetherIsland.Core`, `AetherIsland.Core.Models`,
  `AetherIsland.Core.Logic`, `AetherIsland.Data`, `AetherIsland.Windows`, `AetherIsland.Integrations.*`

## Service API (see Services/AppServices.cs)
- `AppServices.Settings` (Core.Models.AppSettings, mutable snapshot), `AppServices.SettingsStore` (Load()/Save(settings))
- `AppServices.Database` (Data.Db) → repos: `TaskRepository(Db)`, `NoteRepository(Db)`,
  `FileShelfRepository(Db)`, `ScreenTimeRepository(Db)`, `FocusRepository(Db)`, `HealthLogRepository(Db)`,
  `AiCacheRepository(Db)` — see Data/Repositories.cs for methods.
- `AppServices.Notify.ShowBalloon(title, msg)`, `AppServices.Notify.ShowIslandContext(glyph, msg)`
- `AppServices.Weather` (Current: WeatherData?, RefreshAsync(), Updated event)
- `AppServices.Media` (SmtcMediaWatcher: `LastSession`, `Updated` event, `TogglePlayPauseAsync()`)
- `AppServices.CalendarProvider.LoadAsync(urls)` → List<CalendarEvent> (Title, Start, End, AllDay, Location, Url, Source)
- `AppServices.Ai` (AiUsageService: `Aggregator` (UsageAggregator: Results, OfficialTotals, LocalTotals),
  `RefreshAsync()`, `Agents` (AgentDetector.Detect()), static `DisplayNameFor(id)`)
- `AppServices.Health` (HealthService: HydrationToday, AddHydration(ml), ResetHydration(), ReminderDue event)
- `AppServices.Launcher` (LauncherStore: Load()/Save(List<AppShortcut>)) — AppShortcut {Name, Path}
- Windows interop (`AetherIsland.Windows`): `PowerHelper` (LockWorkstation, SendMediaKey, KeepAwake/AllowSleep),
  `AudioEndpoints` (GetSpeaker/GetMicrophone → AudioDeviceState(Available, Muted, VolumePercent), ToggleSpeakerMute/ToggleMicrophoneMute/SetSpeakerMute...),
  `WlanApi` (GetAvailableNetworks → List<WiFiNetwork(Ssid, ProfileName, SignalPercent, Secured, Connected, Connectable)>,
  EnumerateInterfaces, ConnectOpen(ssid), Disconnect), `ForegroundTracker` (GetForegroundApp, GetIdleSeconds),
  `SystemMetricsSampler` (Sample() → SystemSample(CpuPercent, MemoryPercent, MemoryUsedBytes, MemoryTotalBytes,
  NetUpBps, NetDownBps, HasBattery, BatteryPercent, Charging, BatteryLifeSeconds, CpuTemperatureCelsius)), `Disks.GetDisks()`.
- Shell icon thumbnails: `ShellIcons.GetFileIconHandle(path, large)` → HICON (must DestroyIconHandle after) —
  convert with `Imaging.CreateBitmapSourceFromHIcon` and Freeze().
- Screenshots: `System.Drawing.Graphics.CopyFromScreen` (see UtilitiesViewModel.CaptureScreen once written —
  if UtilitiesViewModel does not exist yet, implement a local static `CaptureScreen(string path)` yourself).

## Lifecycle
VMs implement `AetherIsland.App.ViewModels.ILifecycleAware` (`OnShown()` / `OnHidden()`) when they
need refresh timers or sampling. The shell calls these on navigation. Heavy polling MUST stop in OnHidden().

## Localization
`AetherIsland.Core.Loc.T("key")` / `Loc.TF("key", args)` (see Core/Localization/strings.en.json for keys —
that file is authoritative; do not invent keys). Bind localized text through VM string properties
(computed in the constructor), NOT through XAML. Example: `public string RefreshLabel => Loc.T("common.refresh");`

## Existing XAML resources (Themes/Controls.xaml + Dark.xaml)
- Brushes: BgBrush, SurfaceBrush, Surface2Brush, HoverBrush, TextBrush, Text2Brush, MutedBrush,
  AccentBrush, AiBrush, SuccessBrush, WarningBrush, DangerBrush, StrokeBrush, AccentDynamicBrush
- Styles: `SectionTitle`, `Subtle`, `Muted`, `Numeric` (tabular figures), `Glyph` (icon font TextBlock),
  `IconButton`, `PrimaryButton`, `GhostButton`, `DangerButton`, `NavItem` (RadioButton),
  `ToggleTile` (ToggleButton; set `Tag` to glyph, `AutomationProperties.Name` to label),
  `SlimProgress` (ProgressBar), `DarkTextBox`, `WidgetCard` (Border style)
- Fonts: UiFont, GlyphFont (icon glyphs, e.g. play "\uE768", pause "\uE769", refresh "\uE72C",
  settings "\uE713", search "\uE721", add "\uE710", delete "\uE74D", pin "\uE840", folder "\uE8B7"),
  NumericFont. Outer corner radius token: OuterCornerRadius; cards: WidgetCornerRadius.
- Panel width ≈ 860, content height ≈ 360 after the nav rail (~48px). Design for a compact,
  information-dense dark UI with 8px rhythm. Everything must fit without scrolling where possible;
  use one ScrollViewer around the main content for module pages.

## Shell integration
- The shell (IslandWindow.xaml) maps VM type → View via implicit DataTemplates, so VM and View
  class names must match exactly: `TasksViewModel`↔`TasksView`, `NotesViewModel`↔`NotesView`, etc.
- View roots: no Width/Height (fills content area), `Background="Transparent"`.
- Use `{Binding}` only against public members of your VM.

## Quality bar (spec §18/§22)
- Every list needs an honest empty state; every network call needs error text and a retry.
- Never fabricate data. Show "—" or the localized "not available" text when a source is unavailable.
- Keep code compact; match the existing style (see DashboardViewModels.cs for the house style).
