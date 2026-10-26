using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.App.Services;
using AetherIsland.Core;

namespace AetherIsland.App.Windows;

public partial class OnboardingWindow : Window
{
    public OnboardingViewModel VM { get; } = new();

    public OnboardingWindow()
    {
        InitializeComponent();
        DataContext = VM;
        FlowDirection = ThemeService.FlowDirectionForCurrentLanguage();
        VM.Finished += () => Close(); // island is already running behind
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        AppServices.Theme.Apply();
        FlowDirection = ThemeService.FlowDirectionForCurrentLanguage();
        VM.RaiseLabelsChanged();
    }
}

public partial class OnboardingViewModel : ObservableObject
{
    private readonly List<OnboardingStep> _steps;
    private int _index;

    public event Action? Finished;

    [ObservableProperty] private OnboardingStep? _currentStep;
    [ObservableProperty] private string _nextLabel = Loc.T("ob.next");
    [ObservableProperty] private string _backLabel = Loc.T("ob.back");
    [ObservableProperty] private string _skipLabel = Loc.T("ob.skip");

    public string[] Languages { get; } = { "en", "ar", "tr" };
    public string[] Themes { get; } = { "dark", "light", "system" };

    // step visibility
    public bool ShowAppearance => CurrentStep?.Kind == OnboardingStepKind.Appearance;
    public bool ShowBehavior => CurrentStep?.Kind == OnboardingStepKind.Behavior;
    public bool ShowPrivacy => CurrentStep?.Kind == OnboardingStepKind.Privacy;
    public bool ShowDone => CurrentStep?.Kind == OnboardingStepKind.Done;

    // labels
    public string ThemeLabel => Loc.T("set.theme");
    public string LanguageLabel => Loc.T("set.language");
    public string HideFullscreenLabel => Loc.T("set.fullscreen");
    public string CollapseLabel => Loc.T("set.collapseDesc");
    public string ScreenTimeLabel => Loc.T("set.screenTime");
    public string AgentMetaLabel => Loc.T("set.agentMeta");
    public string HotkeyHintText => $"{Loc.T("ob.hotkeyHint")}:  {AppServices.Settings.Shortcuts.ToggleIsland}";

    public string SelectedTheme
    {
        get => AppServices.Settings.Appearance.Theme;
        set { AppServices.Settings.Appearance.Theme = value; }
    }

    public string SelectedLanguage
    {
        get => AppServices.Settings.General.Language;
        set { AppServices.Settings.General.Language = value; }
    }

    public bool HideInFullscreen
    {
        get => AppServices.Settings.Behavior.FullscreenRule == "hide";
        set => AppServices.Settings.Behavior.FullscreenRule = value ? "hide" : "stay";
    }

    public bool CollapseOnDeactivate
    {
        get => AppServices.Settings.Behavior.CollapseOnDeactivate;
        set => AppServices.Settings.Behavior.CollapseOnDeactivate = value;
    }

    public bool ScreenTimeConsent
    {
        get => AppServices.Settings.Privacy.ScreenTimeConsent;
        set
        {
            AppServices.Settings.Privacy.ScreenTimeConsent = value;
            AppServices.ScreenTime.SetConsent(value);
        }
    }

    public bool AgentMetaConsent
    {
        get => AppServices.Settings.Privacy.AgentMetadataConsent;
        set => AppServices.Settings.Privacy.AgentMetadataConsent = value;
    }

    partial void OnCurrentStepChanged(OnboardingStep? value)
    {
        OnPropertyChanged(nameof(ShowAppearance));
        OnPropertyChanged(nameof(ShowBehavior));
        OnPropertyChanged(nameof(ShowPrivacy));
        OnPropertyChanged(nameof(ShowDone));
    }

    public OnboardingViewModel()
    {
        var s = AppServices.Settings;
        _steps = new List<OnboardingStep>
        {
            new(Loc.T("ob.welcome"), Loc.T("ob.welcomeBody"), OnboardingStepKind.Text),
            new(Loc.T("ob.appearance"), Loc.T("ob.appearanceBody"), OnboardingStepKind.Appearance),
            new(Loc.T("ob.behavior"), Loc.T("ob.behaviorBody"), OnboardingStepKind.Behavior),
            new(Loc.T("ob.modules"), Loc.T("ob.modulesBody"), OnboardingStepKind.Modules),
            new(Loc.T("ob.privacy"), Loc.T("ob.privacyBody"), OnboardingStepKind.Privacy),
            new(Loc.T("ob.done"), Loc.T("ob.doneBody"), OnboardingStepKind.Done),
        };
        _index = 0;
        CurrentStep = _steps[0];
        UpdateNextLabel();
    }

    [RelayCommand]
    private void Next()
    {
        if (_index >= _steps.Count - 1) { Finish(); return; }
        _index++;
        CurrentStep = _steps[_index];
        UpdateNextLabel();
    }

    [RelayCommand]
    private void Back()
    {
        if (_index == 0) return;
        _index--;
        CurrentStep = _steps[_index];
        UpdateNextLabel();
    }

    [RelayCommand]
    private void Skip() => Finish();

    private void UpdateNextLabel() =>
        NextLabel = _index >= _steps.Count - 1 ? Loc.T("ob.finish") : Loc.T("ob.next");

    public void RaiseLabelsChanged()
    {
        OnPropertyChanged(nameof(ScreenTimeLabel));
        OnPropertyChanged(nameof(AgentMetaLabel));
        OnPropertyChanged(nameof(HideFullscreenLabel));
        OnPropertyChanged(nameof(CollapseLabel));
        OnPropertyChanged(nameof(HotkeyHintText));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(BackLabel));
        OnPropertyChanged(nameof(SkipLabel));
    }

    private void Finish()
    {
        var s = AppServices.Settings;
        s.General.OnboardingCompleted = true;
        AppServices.SettingsStore.Save(s);
        AppServices.Theme.Apply();
        Finished?.Invoke();
    }
}

public enum OnboardingStepKind { Text, Appearance, Behavior, Modules, Privacy, Done }

public sealed class OnboardingStep
{
    public string Title { get; }
    public string Body { get; }
    public OnboardingStepKind Kind { get; }

    public OnboardingStep(string title, string body, OnboardingStepKind kind)
    {
        Title = title;
        Body = body;
        Kind = kind;
    }
}
