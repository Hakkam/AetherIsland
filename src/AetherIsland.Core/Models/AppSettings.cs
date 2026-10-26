using System.Text.Json.Serialization;

namespace AetherIsland.Core.Models;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public DashboardSettings Dashboard { get; set; } = new();
    public DaySchedule Day { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public WeatherSettings Weather { get; set; } = new();
    public FocusSettings Focus { get; set; } = new();
    public HealthSettings Health { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public ShortcutSettings Shortcuts { get; set; } = new();
    public CalendarSettings Calendar { get; set; } = new();
}

public sealed class GeneralSettings
{
    public string Language { get; set; } = "en"; // en | ar | tr
    public bool StartWithWindows { get; set; }
    public bool OnboardingCompleted { get; set; }
    public string DefaultModuleId { get; set; } = "dashboard";
    public bool ToastNotifications { get; set; } = true;
    public List<string> ModuleOrder { get; set; } = new();
    public List<string> HiddenModules { get; set; } = new();
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "dark"; // dark | light | system
    public string AccentHex { get; set; } = "#32A8FF";
    public bool UseBlur { get; set; } = true;
    public double PanelOpacity { get; set; } = 1.0;
    public double CornerRadius { get; set; } = 24;
    public double ExpandedWidth { get; set; } = 860;
    public double ExpandedHeight { get; set; } = 430;
}

public sealed class BehaviorSettings
{
    public bool Pinned { get; set; }
    public bool CollapseOnDeactivate { get; set; } = true;
    public string FullscreenRule { get; set; } = "hide"; // hide | stay
    public string? MonitorDeviceName { get; set; }
    public bool ReducedMotion { get; set; }
    // Desktop-independent offsets relative to the selected monitor working area.
    // Null retains the default centered position near the top of the display.
    public double? IslandCenterOffsetX { get; set; }
    public double? IslandTopOffsetY { get; set; }
}

public sealed class DashboardSettings
{
    public List<string> WidgetOrder { get; set; } = new() { "weather", "toggles", "apps", "dayprogress" };
    public List<string> HiddenWidgets { get; set; } = new();
}

public sealed class DaySchedule
{
    public string WakeTime { get; set; } = "07:00";
    public string WorkStart { get; set; } = "09:00";
    public string WorkEnd { get; set; } = "17:00";
    public string BedTime { get; set; } = "23:30";
    public bool Use24Hour { get; set; } = true;
}

public sealed class PrivacySettings
{
    public bool ScreenTimeConsent { get; set; }
    public bool AgentMetadataConsent { get; set; }
    public bool CameraConsent { get; set; }
    public int RetentionDays { get; set; } = 30;
}

public sealed class WeatherSettings
{
    public string LocationName { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Units { get; set; } = "metric"; // metric | imperial
}

public sealed class FocusSettings
{
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLongBreak { get; set; } = 4;
    public bool DesktopNotifications { get; set; } = true;
}

public sealed class HealthSettings
{
    public bool RemindersEnabled { get; set; } = true;
    public int EyeBreakMinutes { get; set; } = 20;
    public int MoveMinutes { get; set; } = 60;
    public int HydrationGoalMl { get; set; } = 2000;
    public int HydrationCupMl { get; set; } = 250;
}

public sealed class AiProviderSettings
{
    public string ProviderId { get; set; } = "";
    public bool Enabled { get; set; }
    public string? AccountLabel { get; set; }
    public string? ApiBaseUrl { get; set; }          // optional override / local server URL
    public double? ManualMonthlyBudgetUsd { get; set; }
    public bool IncludeInTotals { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string? OrganizationId { get; set; }
}

public sealed class AiSettings
{
    public List<AiProviderSettings> Providers { get; set; } = new();
    public bool AlertsEnabled { get; set; } = true;
    public List<int> AlertThresholdsPercent { get; set; } = new() { 50, 80, 90, 100 };
    public List<string> MutedAlerts { get; set; } = new();
    public int PollMinutes { get; set; } = 15;
    public bool ShowCompactStatus { get; set; } = true;
}

public sealed class ShortcutSettings
{
    public string ToggleIsland { get; set; } = "Ctrl+Alt+Space";
    public string OpenAiCenter { get; set; } = "Ctrl+Alt+A";
    public string OpenFileShelf { get; set; } = "Ctrl+Alt+F";
    public string StartPauseTimer { get; set; } = "Ctrl+Alt+T";
}

public sealed class CalendarSettings
{
    public List<string> IcsUrls { get; set; } = new();
    public string GoogleClientId { get; set; } = "";
    public string MicrosoftClientId { get; set; } = "";
}

public sealed class TaskItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public bool Completed { get; set; }
    public int Priority { get; set; } // 0 normal, 1 high, 2 low
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public bool Deleted { get; set; }
}

public sealed class NoteItem
{
    public long Id { get; set; }
    public string Content { get; set; } = "";
    public bool Pinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Title
    {
        get
        {
            var first = Content.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            return string.IsNullOrWhiteSpace(first) ? "Untitled" : first.Trim();
        }
    }
}

public sealed class ShelfFile
{
    public long Id { get; set; }
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
