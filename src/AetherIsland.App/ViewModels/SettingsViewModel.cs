using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.App.Services;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.AI;
using Microsoft.Win32;

namespace AetherIsland.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public AppSettings S { get; }
    private string _lastLanguage = "";

    // labels
    public string SettingsTitle => Loc.T("common.settings");
    public string GeneralTitle => Loc.T("set.general");
    public string AppearanceTitle => Loc.T("set.appearance");
    public string IslandTitle => Loc.T("set.island");
    public string AiProvidersTitle => Loc.T("set.aiProviders");
    public string DashboardTitle => Loc.T("set.dashboard");
    public string ModulesTitle => Loc.T("set.modules");
    public string WidgetHint => Loc.T("set.widgetHint");
    public string ModuleHint => Loc.T("set.moduleHint");
    public string ResetWidgetsLabel => Loc.T("set.resetWidgets");
    public string VerifiedProviderHint => Loc.T("set.verifiedProviderOnly");
    public string PrivacyTitle => Loc.T("set.privacy");
    public string ShortcutsTitle => Loc.T("set.shortcuts");
    public string DataTitle => Loc.T("set.data");
    public string AboutTitle => Loc.T("set.about");
    public string LanguageLabel => Loc.T("set.language");
    public string ThemeLabel => Loc.T("set.theme");
    public string DarkLabel => Loc.T("set.dark");
    public string LightLabel => Loc.T("set.light");
    public string SystemLabel => Loc.T("set.system");
    public string AccentLabel => Loc.T("set.accent");
    public string StartupLabel => Loc.T("set.startup");
    public string StartupDesc => Loc.T("set.startupDesc");
    public string CornerLabel => Loc.T("set.corner");
    public string WidthLabel => Loc.T("set.width");
    public string HeightLabel => Loc.T("set.height");
    public string OpacityLabel => Loc.T("set.opacity");
    public string PinnedLabel => Loc.T("set.pinned");
    public string CollapseLabel => Loc.T("set.collapseDesc");
    public string FullscreenLabel => Loc.T("set.fullscreen");
    public string MonitorLabel => Loc.T("set.monitor");
    public string[] MonitorOptions => new[] { Loc.T("set.primaryMonitor") }.Concat(MonitorService.DeviceNames()).ToArray();
    public string SelectedMonitor
    {
        get => S.Behavior.MonitorDeviceName ?? Loc.T("set.primaryMonitor");
        set => S.Behavior.MonitorDeviceName = value == Loc.T("set.primaryMonitor") ? null : value;
    }
    public string ScreenTimeLabel => Loc.T("set.screenTime");
    public string AgentMetaLabel => Loc.T("set.agentMeta");
    public string RetentionLabel => Loc.T("set.retention");
    public string DaysLabel => Loc.T("set.days");
    public string ExportLabel => Loc.T("set.export");
    public string ImportLabel => Loc.T("set.import");
    public string ResetLabel => Loc.T("set.reset");
    public string VersionLabel => Loc.T("set.version");
    public string PollLabel => Loc.T("set.pollMinutes");
    public string BudgetLabel => Loc.T("ai.budgetMonthly");
    public string ApiKeyLabel => Loc.T("ai.apiKey");
    public string ApiKeySecure => Loc.T("ai.apiKeySecure");
    public string TestLabel => Loc.T("ai.testConnection");
    public string AddLabel => Loc.T("ai.addProvider");
    public string DisconnectLabel => Loc.T("ai.disconnect");
    public string VersionText => "Aether Island 1.0.0 آ· " + Loc.T("set.version");

    public string[] Languages { get; } = { "en", "ar", "tr" };
    public string[] Themes { get; } = { "dark", "light", "system" };
    public static string[] AiProviderIdOptions { get; } = { AiProviderIds.OpenAI };
    public static string[] AiProviderNameOptions { get; } = { "OpenAI API organization" };

    public ObservableCollection<AiProviderRowVm> ProviderRows { get; } = new();
    public ObservableCollection<ModuleOptionVm> ModuleRows { get; } = new();
    public ObservableCollection<WidgetOptionVm> WidgetRows { get; } = new();

    [ObservableProperty] private string _newProviderId = AiProviderIds.OpenAI;
    [ObservableProperty] private string _newProviderKey = "";
    [ObservableProperty] private string _newProviderBaseUrl = "";
    [ObservableProperty] private string _newProviderBudget = "";
    [ObservableProperty] private string _statusText = "";

    public bool HideInFullscreen
    {
        get => S.Behavior.FullscreenRule == "hide";
        set => S.Behavior.FullscreenRule = value ? "hide" : "stay";
    }

    public int NewProviderIndex
    {
        get => Array.IndexOf(AiProviderIdOptions, NewProviderId);
        set
        {
            if (value >= 0 && value < AiProviderIdOptions.Length)
                NewProviderId = AiProviderIdOptions[value];
            OnPropertyChanged(nameof(NewProviderIndex));
        }
    }

    public SettingsViewModel()
    {
        S = AppServices.Settings;
        _lastLanguage = S.General.Language;
        ReloadProviderRows();
        LoadModules();
        LoadWidgets();
    }

    private void LoadModules()
    {
        ModuleRows.Clear();
        var ids = new[] { "home", "dashboard", "fileshelf", "tasks", "notes", "focus", "media", "calendar", "weather", "productivity", "network", "system", "wellbeing", "ai", "utilities" };
        var ordered = (S.General.ModuleOrder ?? new()).Concat(ids).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ordered.Where(id => ids.Contains(id)))
            ModuleRows.Add(new ModuleOptionVm(id, Loc.T("nav." + id),
                !(S.General.HiddenModules ?? new()).Contains(id), this));
    }

    internal void ApplyModules()
    {
        S.General.ModuleOrder = ModuleRows.Select(row => row.Id).ToList();
        S.General.HiddenModules = ModuleRows.Where(row => !row.IsVisible).Select(row => row.Id).ToList();
        Save();
        if (((App)System.Windows.Application.Current).CurrentMainWindow is Windows.IslandWindow island)
            island.RebuildNavigation();
    }

    internal void MoveModule(ModuleOptionVm row, int offset)
    {
        var index = ModuleRows.IndexOf(row);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= ModuleRows.Count) return;
        ModuleRows.Move(index, target);
        ApplyModules();
    }

    private void LoadWidgets()
    {
        WidgetRows.Clear();
        var ids = new[] { "weather", "toggles", "apps", "dayprogress" };
        foreach (var id in (S.Dashboard.WidgetOrder ?? new()).Concat(ids).Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(id => ids.Contains(id)))
            WidgetRows.Add(new WidgetOptionVm(id, Loc.T("dash." + id),
                !(S.Dashboard.HiddenWidgets ?? new()).Contains(id), this));
    }

    internal void ApplyWidgets()
    {
        S.Dashboard.WidgetOrder = WidgetRows.Select(row => row.Id).ToList();
        S.Dashboard.HiddenWidgets = WidgetRows.Where(row => !row.IsVisible).Select(row => row.Id).ToList();
        Save();
        DashboardViewModel.Instance?.NotifyLayoutChanged();
    }

    internal void MoveWidget(WidgetOptionVm row, int offset)
    {
        var index = WidgetRows.IndexOf(row);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= WidgetRows.Count) return;
        WidgetRows.Move(index, target);
        ApplyWidgets();
    }

    [RelayCommand]
    private void ResetWidgets()
    {
        S.Dashboard = new DashboardSettings();
        LoadWidgets();
        ApplyWidgets();
    }

    public void Save()
    {
        AppServices.SettingsStore.Save(S);
        AppServices.Theme.Apply();
        if (_lastLanguage != S.General.Language)
        {
            _lastLanguage = S.General.Language;
            OnPropertyChanged(string.Empty);
            ReloadProviderRows();
            LoadModules();
            LoadWidgets();
        }
        AppServices.ScreenTime.SetConsent(S.Privacy.ScreenTimeConsent);
        ApplyStartupRegistration();
        ((App)System.Windows.Application.Current).RestartHotkeys();
        if (((App)System.Windows.Application.Current).CurrentMainWindow is Windows.IslandWindow island)
            island.Reposition();
    }

    private void ApplyStartupRegistration()
    {
        try
        {
            const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string name = "AetherIsland";
            using var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key, writable: true);
            if (rk is null) return;
            if (S.General.StartWithWindows)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe)) rk.SetValue(name, $"\"{exe}\"");
            }
            else rk.DeleteValue(name, throwOnMissingValue: false);
        }
        catch { }
    }

    // ----- AI providers -----

    private void ReloadProviderRows()
    {
        ProviderRows.Clear();
        foreach (var p in S.Ai.Providers.Where(p => p.ProviderId == AiProviderIds.OpenAI))
            ProviderRows.Add(new AiProviderRowVm(p, this));
    }

    [RelayCommand]
    private void AddProvider()
    {
        if (NewProviderId != AiProviderIds.OpenAI) { StatusText = "This provider has no supported usage limit"; return; }
        if (string.IsNullOrWhiteSpace(NewProviderKey)) { StatusText = "An OpenAI organization admin key is required"; return; }
        if (S.Ai.Providers.Any(p => p.ProviderId == NewProviderId)) { StatusText = "Already connected"; return; }
        var p = new AiProviderSettings
        {
            ProviderId = NewProviderId,
            Enabled = true,
            ApiBaseUrl = null,
            ManualMonthlyBudgetUsd = null
        };
        S.Ai.Providers.Add(p);
        if (!string.IsNullOrWhiteSpace(NewProviderKey))
            AppServices.CredentialStore.SetSecret(NewProviderId, NewProviderKey.Trim());
        NewProviderKey = ""; NewProviderBaseUrl = ""; NewProviderBudget = "";
        Save();
        ReloadProviderRows();
        StatusText = Loc.T("set.keyStored");
        _ = AppServices.Ai.RefreshAsync();
    }

    [RelayCommand]
    private void RemoveProvider(AiProviderRowVm row)
    {
        var p = S.Ai.Providers.FirstOrDefault(x => x.ProviderId == row.ProviderId);
        if (p is null) return;
        S.Ai.Providers.Remove(p);
        AppServices.CredentialStore.RemoveSecret(row.ProviderId);
        Save();
        ReloadProviderRows();
    }

    [RelayCommand]
    public async Task TestProviderAsync(AiProviderRowVm row)
    {
        var p = S.Ai.Providers.FirstOrDefault(x => x.ProviderId == row.ProviderId);
        if (p is null) return;
        StatusText = "â€¦";
        var adapter = AppServices.Ai.BuildProvider(p);
        if (adapter is null) { StatusText = "Unknown provider"; return; }
        var status = await adapter.CheckConnectionAsync(System.Threading.CancellationToken.None);
        var quotas = status.Connected ? await adapter.GetQuotaWindowsAsync(System.Threading.CancellationToken.None) : Array.Empty<QuotaWindow>();
        if (status.Connected && !quotas.Any(q => q.Source == UsageSource.VerifiedOfficial))
            status.Message += "; no enforced monthly spend limit is accessible";
        row.StatusText = status.State switch
        {
            "connected" => Loc.T("common.connected"),
            "unauthorized" => Loc.T("common.error"),
            _ => Loc.T("common.disconnected")
        } + " آ· " + status.Message;
        StatusText = status.Message;
    }

    // ----- data -----

    [RelayCommand]
    private void ExportSettings()
    {
        var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "aether-island-settings.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName,
                System.Text.Json.JsonSerializer.Serialize(S, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            StatusText = "Exported";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    [RelayCommand]
    private void ImportSettings()
    {
        var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var imported = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.IO.File.ReadAllText(dlg.FileName));
            if (imported is null) return;
            // credentials are never exported/imported
            var providers = S.Ai.Providers;
            imported.Ai = new AiSettings { Providers = providers };
            ApplyImportedSettings(imported);
            StatusText = "Imported";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    private void ApplyImportedSettings(AppSettings source)
    {
        // Services and view models retain the original settings instance.
        S.General = source.General ?? new();
        S.Appearance = source.Appearance ?? new();
        S.Behavior = source.Behavior ?? new();
        S.Dashboard = source.Dashboard ?? new();
        S.Day = source.Day ?? new();
        S.Privacy = source.Privacy ?? new();
        S.Weather = source.Weather ?? new();
        S.Focus = source.Focus ?? new();
        S.Health = source.Health ?? new();
        S.Ai = source.Ai ?? new();
        S.Shortcuts = source.Shortcuts ?? new();
        S.Calendar = source.Calendar ?? new();
        Save();
        OnPropertyChanged(string.Empty);
        ReloadProviderRows();
        LoadModules();
        LoadWidgets();
        if (((App)System.Windows.Application.Current).CurrentMainWindow is Windows.IslandWindow island)
            island.RebuildNavigation();
        DashboardViewModel.Instance?.NotifyLayoutChanged();
    }

    [RelayCommand]
    private void ResetSettings()
    {
        foreach (var provider in S.Ai.Providers)
            AppServices.CredentialStore.RemoveSecret(provider.ProviderId);
        ApplyImportedSettings(new AppSettings());
        StatusText = "Reset";
    }
}

public partial class AiProviderRowVm : ObservableObject
{
    public string ProviderId { get; }
    public string DisplayName { get; }
    public bool HasKey { get; }
    public bool IsLocal { get; }
    public string RemoveLabel { get; } = Loc.T("ai.disconnect");
    public string TestLabel { get; } = Loc.T("ai.testConnection");

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private double? _budget;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _lastSyncText = "";

    public SettingsViewModel Owner { get; }

    public string StatusHint => HasKey ? Loc.T("set.keyStored") : IsLocal ? Loc.T("set.localDir") : "â€”";
    public string BudgetLabel => Loc.T("ai.budgetMonthly");

    public AiProviderRowVm(AiProviderSettings p, SettingsViewModel owner)
    {
        ProviderId = p.ProviderId;
        DisplayName = AiUsageService.DisplayNameFor(p.ProviderId);
        HasKey = AppServices.CredentialStore.GetSecret(p.ProviderId) is not null;
        IsLocal = p.ProviderId is AiProviderIds.OpenCode or AiProviderIds.OpenAiCodex or AiProviderIds.ClaudeCode;
        Owner = owner;
        _enabled = p.Enabled;
        _budget = p.ManualMonthlyBudgetUsd;
        LastSyncText = p.LastSyncAt is { } t ? $"{Loc.T("ai.lastSync")}: {t:H:mm}" : "";
    }

    partial void OnEnabledChanged(bool value)
    {
        var p = AppServices.Settings.Ai.Providers.FirstOrDefault(x => x.ProviderId == ProviderId);
        if (p is not null) { p.Enabled = value; Owner.Save(); }
    }

    partial void OnBudgetChanged(double? value)
    {
        var p = AppServices.Settings.Ai.Providers.FirstOrDefault(x => x.ProviderId == ProviderId);
        if (p is not null) { p.ManualMonthlyBudgetUsd = value; Owner.Save(); }
    }
}


public partial class ModuleOptionVm : ObservableObject
{
    private readonly SettingsViewModel _owner;
    public string Id { get; }
    public string Label { get; }
    public bool CanHide => Id != "dashboard";
    public string MoveUpLabel => Loc.T("set.moveModuleUp");
    public string MoveDownLabel => Loc.T("set.moveModuleDown");
    [ObservableProperty] private bool _isVisible;

    public ModuleOptionVm(string id, string label, bool visible, SettingsViewModel owner)
    {
        Id = id;
        Label = label;
        _owner = owner;
        _isVisible = visible || !CanHide;
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!value && !CanHide) { IsVisible = true; return; }
        _owner.ApplyModules();
    }

    [RelayCommand] private void MoveUp() => _owner.MoveModule(this, -1);
    [RelayCommand] private void MoveDown() => _owner.MoveModule(this, 1);
}


public partial class WidgetOptionVm : ObservableObject
{
    private readonly SettingsViewModel _owner;
    public string Id { get; }
    public string Label { get; }
    public bool CanHide => Id != "weather";
    public string MoveUpLabel => Loc.T("set.moveWidgetUp");
    public string MoveDownLabel => Loc.T("set.moveWidgetDown");
    [ObservableProperty] private bool _isVisible;

    public WidgetOptionVm(string id, string label, bool visible, SettingsViewModel owner)
    {
        Id = id; Label = label; _owner = owner;
        _isVisible = visible || !CanHide;
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!value && !CanHide) { IsVisible = true; return; }
        _owner.ApplyWidgets();
    }

    [RelayCommand] private void MoveUp() => _owner.MoveWidget(this, -1);
    [RelayCommand] private void MoveDown() => _owner.MoveWidget(this, 1);
}
