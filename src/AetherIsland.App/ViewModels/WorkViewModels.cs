using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.Core.Models;
using AetherIsland.Data;

namespace AetherIsland.App.ViewModels;

// ============================ Tasks ============================

public partial class TasksViewModel : ObservableObject, ILifecycleAware
{
    private readonly TaskRepository _repo = new(AppServices.Database);

    [ObservableProperty] private string _newTaskText = "";
    [ObservableProperty] private string _selectedFilter = "active";
    [ObservableProperty] private int _completedTodayCount;
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private ObservableCollection<TaskRowVm> _tasks = new();

    public string AddPlaceholder => Loc.T("tasks.placeholder");
    public string ActiveLabel => Loc.T("tasks.active");
    public string CompletedLabel => Loc.T("tasks.completed");
    public string AllLabel => Loc.T("tasks.all");
    public string ProgressLabel => Loc.T("tasks.progress");
    public string EmptyTextLabel => Loc.T("tasks.empty");
    public bool HasTasks => Tasks.Count > 0;
    public bool NoTasks => !HasTasks;

    partial void OnSelectedFilterChanged(string value) => Reload();

    [RelayCommand]
    private void SetFilter(string filter) => SelectedFilter = filter;

    public void OnShown() => Reload();
    public void OnHidden() { }

    [RelayCommand]
    private void AddTask()
    {
        var title = NewTaskText.Trim();
        if (title.Length == 0) return;
        _repo.Add(new TaskItem { Title = title, CreatedAt = DateTime.Now });
        NewTaskText = "";
        Reload();
    }

    [RelayCommand]
    private void DeleteTask(TaskRowVm row)
    {
        _repo.SoftDelete(row.Id);
        Reload();
    }

    private void Reload()
    {
        var all = _repo.GetAll();
        Tasks.Clear();
        foreach (var t in all)
        {
            var show = SelectedFilter switch
            {
                "active" => !t.Completed,
                "completed" => t.Completed,
                _ => true
            };
            if (show) Tasks.Add(new TaskRowVm(t, this));
        }
        var today = DateTime.Now.Date;
        CompletedTodayCount = all.Count(t => t.Completed && t.CompletedAt is { } c && c.Date == today);
        var activeTotal = all.Count(t => !t.Completed) + CompletedTodayCount;
        ProgressFraction = activeTotal == 0 ? 0 : (double)CompletedTodayCount / activeTotal;
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(NoTasks));
    }

    internal void NotifyChanged() => Reload();
}

public partial class TaskRowVm : ObservableObject
{
    public long Id { get; }
    public TaskItem Model { get; }
    private readonly TasksViewModel _owner;

    public TaskRowVm(TaskItem model, TasksViewModel owner)
    {
        Model = model;
        Id = model.Id;
        _owner = owner;
        _isDone = model.Completed;
    }

    public string Title => Model.Title;
    public string PriorityLabel => Loc.T(Model.Priority switch { 1 => "tasks.high", 2 => "tasks.low", _ => "tasks.normal" });
    public bool IsHigh => Model.Priority == 1;
    public bool IsLow => Model.Priority == 2;
    public string DueText => Model.DueDate is { } d ? $"{Loc.T("tasks.due")} {d:MMM d}" : "";

    [ObservableProperty] private bool _isDone;

    partial void OnIsDoneChanged(bool value)
    {
        Model.Completed = value;
        Model.CompletedAt = value ? DateTime.Now : null;
        _owner.NotifyChanged();
    }

    [RelayCommand]
    private void Toggle()
    {
        Model.Completed = !Model.Completed;
        Model.CompletedAt = Model.Completed ? DateTime.Now : null;
        var repo = new TaskRepository(AppServices.Database);
        repo.Update(Model);
        _owner.NotifyChanged();
    }

    [RelayCommand]
    private void Delete()
    {
        new TaskRepository(AppServices.Database).SoftDelete(Id);
        _owner.NotifyChanged();
    }
}

// ============================ Notes ============================

public partial class NotesViewModel : ObservableObject, ILifecycleAware
{
    private readonly NoteRepository _repo = new(AppServices.Database);
    private readonly DispatcherTimer _saveTimer;
    private bool _loading;

    [ObservableProperty] private ObservableCollection<NoteRowVm> _notes = new();
    [ObservableProperty] private NoteRowVm? _selectedNote;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _editorText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _pinLabel = Loc.T("notes.pin");

    public string NewNoteLabel => Loc.T("notes.new");
    public string SearchHint => Loc.T("notes.search");
    public string EmptyText => Loc.T("notes.empty");
    public bool HasNotes => Notes.Count > 0;

    public NotesViewModel()
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => SaveSelected();
    }

    public void OnShown() => Reload();

    public void OnHidden()
    {
        SaveSelected();
        _saveTimer.Stop();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedNoteChanged(NoteRowVm? value)
    {
        foreach (var n in Notes) n.IsSelected = ReferenceEquals(n, value);
        _saveTimer.Stop();
        if (value is null) { EditorText = ""; return; }
        _loading = true;
        EditorText = value.Model.Content;
        _loading = false;
        PinLabel = Loc.T(value.Model.Pinned ? "notes.unpin" : "notes.pin");
    }

    partial void OnEditorTextChanged(string value)
    {
        if (_loading || SelectedNote is null) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveSelected()
    {
        _saveTimer.Stop();
        var note = SelectedNote;
        if (note is null) return;
        if (note.Model.Content == EditorText) return;
        note.Model.Content = EditorText;
        note.Model.UpdatedAt = DateTime.Now;
        _repo.Update(note.Model.Id, EditorText, note.Model.Pinned);
        note.Refresh();
        StatusText = $"{Loc.T("notes.autoSaved")} {DateTime.Now:H:mm}";
        ApplyFilter(preserveSelection: true);
    }

    [RelayCommand]
    private void NewNote()
    {
        SaveSelected();
        var id = _repo.Add("");
        Reload();
        var created = Notes.FirstOrDefault(n => n.Id == id);
        if (created is not null) SelectedNote = created;
        EditorText = "";
    }

    [RelayCommand]
    private void DeleteNote()
    {
        var note = SelectedNote;
        if (note is null) return;
        _repo.Delete(note.Id);
        SelectedNote = null;
        Reload();
    }

    [RelayCommand]
    private void Pin()
    {
        var note = SelectedNote;
        if (note is null) return;
        note.Model.Pinned = !note.Model.Pinned;
        _repo.Update(note.Model.Id, note.Model.Content, note.Model.Pinned);
        note.Refresh();
        PinLabel = Loc.T(note.Model.Pinned ? "notes.unpin" : "notes.pin");
        Reload();
        var again = Notes.FirstOrDefault(n => n.Id == note.Id);
        if (again is not null) SelectedNote = again;
    }

    private void Reload()
    {
        var items = _repo.GetAll()
            .OrderByDescending(n => n.Pinned)
            .ThenByDescending(n => n.UpdatedAt)
            .Select(n => new NoteRowVm(n))
            .ToList();
        Notes = new ObservableCollection<NoteRowVm>(items);
        ApplyFilter();
        OnPropertyChanged(nameof(HasNotes));
    }

    private void ApplyFilter(bool preserveSelection = false)
    {
        var selectedId = preserveSelection ? SelectedNote?.Id : null;
        var query = SearchText.Trim();
        IEnumerable<NoteRowVm> visible = Notes;
        if (query.Length > 0)
            visible = Notes.Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                       || n.Model.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
        var list = visible.ToList();
        if (preserveSelection)
        {
            var keep = list.FirstOrDefault(n => n.Id == selectedId) ?? list.FirstOrDefault();
            if (!ReferenceEquals(keep, SelectedNote)) SelectedNote = keep;
        }
        else if (SelectedNote is not null && list.All(n => n.Id != SelectedNote.Id))
        {
            SelectedNote = list.FirstOrDefault();
        }
    }
}

public partial class NoteRowVm : ObservableObject
{
    public NoteItem Model { get; }
    public long Id { get; }

    [ObservableProperty] private bool _isSelected;

    public NoteRowVm(NoteItem model)
    {
        Model = model;
        Id = model.Id;
    }

    public string Title => Model.Title;
    public string Preview
    {
        get
        {
            var lines = Model.Content.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(2);
            var joined = string.Join(" · ", lines);
            return joined.Length > 60 ? joined[..60] + "…" : joined;
        }
    }
    public string UpdatedText => Loc.TF("notes.lastEdited", Model.UpdatedAt.ToString("MMM d H:mm"));
    public bool IsPinned => Model.Pinned;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(IsPinned));
    }
}

// ============================ File Shelf ============================

public partial class FileShelfViewModel : ObservableObject, ILifecycleAware
{
    private readonly FileShelfRepository _repo = new(AppServices.Database);

    [ObservableProperty] private ObservableCollection<ShelfItemVm> _items = new();
    [ObservableProperty] private string _searchText = "";

    public string EmptyTitle => Loc.T("shelf.dropHere");
    public string EmptyHint => Loc.T("shelf.dropHint");
    public string MissingText => Loc.T("shelf.missing");
    public string CountText => Loc.TF("shelf.count", Items.Count);
    public string ClearLabel => Loc.T("shelf.clear");
    public bool HasItems => Filtered.Count() > 0;
    public bool EmptyVisible => !HasItems;

    public IEnumerable<ShelfItemVm> Filtered
    {
        get
        {
            var q = SearchText?.Trim() ?? "";
            return string.IsNullOrEmpty(q)
                ? Items
                : Items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(Filtered));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyVisible));
    }

    public void OnShown() => Refresh();
    public void OnHidden() { }

    public void Refresh()
    {
        Items.Clear();
        foreach (var f in _repo.GetAll())
            Items.Add(new ShelfItemVm(f, this));
        OnPropertyChanged(nameof(Filtered));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyVisible));
        OnPropertyChanged(nameof(CountText));
    }

    public void AddDroppedPaths(IEnumerable<string?> paths)
    {
        var added = false;
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)!))
        {
            var p = path!;
            try
            {
                var isDir = Directory.Exists(p);
                if (!isDir && !File.Exists(p)) continue;
                var fi = new FileInfo(p);
                var entry = new ShelfFile
                {
                    Path = p,
                    Name = isDir ? new DirectoryInfo(p).Name : fi.Name,
                    IsDirectory = isDir,
                    SizeBytes = isDir ? 0 : fi.Length,
                    AddedAt = DateTime.Now
                };
                added |= _repo.Add(entry);
            }
            catch { }
        }
        if (added)
        {
            Refresh();
            AppServices.Notify.ShowIslandContext("\uE8B7", Loc.T("notif.fileAdded"));
        }
    }

    [RelayCommand]
    private void Open(ShelfItemVm item)
    {
        if (item.IsMissing) { AppServices.Notify.ShowIslandContext("\uE8B7", Loc.T("shelf.missing")); return; }
        try { Process.Start(new ProcessStartInfo(item.Model.Path) { UseShellExecute = true }); }
        catch (Exception ex) { AppServices.Notify.ShowBalloon("Aether Island", ex.Message); }
    }

    [RelayCommand]
    private void Reveal(ShelfItemVm item)
    {
        if (item.IsMissing) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Model.Path}\""));
        }
        catch { }
    }

    [RelayCommand]
    private void CopyPath(ShelfItemVm item) => Clipboard.SetText(item.Model.Path);

    [RelayCommand]
    private void CopyFile(ShelfItemVm item)
    {
        if (item.IsMissing) return;
        var list = new System.Collections.Specialized.StringCollection { item.Model.Path };
        Clipboard.SetFileDropList(list);
    }

    [RelayCommand]
    private void Remove(ShelfItemVm item)
    {
        _repo.Remove(item.Model.Id);
        Refresh();
    }

    [RelayCommand]
    private void ClearAll()
    {
        _repo.Clear();
        Refresh();
    }

    public void BeginDragOut(ShelfItemVm item, UIElement source)
    {
        var paths = Filtered.Where(i => !i.IsMissing).Select(i => i.Model.Path).ToArray();
        if (paths.Length == 0) return;
        var data = new DataObject(DataFormats.FileDrop, paths);
        try { DragDrop.DoDragDrop(source, data, DragDropEffects.Copy); } catch { }
    }
}

public partial class ShelfItemVm : ObservableObject
{
    public ShelfFile Model { get; }
    private readonly FileShelfViewModel _owner;

    public ShelfItemVm(ShelfFile model, FileShelfViewModel owner)
    {
        Model = model;
        _owner = owner;
        Thumbnail = LoadThumbnail();
    }

    public string Name => Model.Name;
    public string Path => Model.Path;
    public bool IsMissing => !Model.IsDirectory && !File.Exists(Model.Path);
    public bool IsImage => !Model.IsDirectory && Path.ToLowerInvariant() is { } ext &&
        (ext.EndsWith(".png") || ext.EndsWith(".jpg") || ext.EndsWith(".jpeg") ||
         ext.EndsWith(".gif") || ext.EndsWith(".bmp") || ext.EndsWith(".webp"));
    public BitmapSource? Thumbnail { get; }
    public string SizeText => Model.IsDirectory
        ? "—"
        : Model.SizeBytes switch
        {
            >= 1_073_741_824 => $"{Model.SizeBytes / 1_073_741_824.0:0.#} GB",
            >= 1_048_576 => $"{Model.SizeBytes / 1_048_576.0:0.#} MB",
            >= 1_024 => $"{Model.SizeBytes / 1_024.0:0.#} KB",
            _ => $"{Model.SizeBytes} B"
        };
    public string AddedText => Model.AddedAt.ToString("MMM d H:mm");
    public bool NoThumb => Thumbnail is null;

    public string OpenLabel => Loc.T("common.open");
    public string RevealLabel => Loc.T("shelf.reveal");
    public string CopyPathLabel => Loc.T("shelf.copyPath");
    public string CopyFileLabel => Loc.T("shelf.copyFile");
    public string RemoveLabel => Loc.T("common.remove");

    [RelayCommand]
    private void Open() => _owner.OpenCommand.Execute(this);

    [RelayCommand]
    private void Reveal() => _owner.RevealCommand.Execute(this);

    [RelayCommand]
    private void CopyPath() => _owner.CopyPathCommand.Execute(this);

    [RelayCommand]
    private void CopyFile() => _owner.CopyFileCommand.Execute(this);

    [RelayCommand]
    private void Remove() => _owner.RemoveCommand.Execute(this);

    private BitmapSource? LoadThumbnail()
    {
        try
        {
            if (IsImage)
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.DecodePixelWidth = 64;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(Model.Path);
                img.EndInit();
                img.Freeze();
                return img;
            }
            var hIcon = AetherIsland.Windows.ShellIcons.GetFileIconHandle(Model.Path, large: true);
            if (hIcon == IntPtr.Zero) return null;
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            AetherIsland.Windows.ShellIcons.DestroyIconHandle(hIcon);
            return src;
        }
        catch
        {
            return null;
        }
    }
}

// ============================ Focus / Pomodoro ============================

public partial class FocusViewModel : ObservableObject, ILifecycleAware
{
    private readonly FocusRepository _repo = new(AppServices.Database);
    private readonly DispatcherTimer _timer;
    private DateTime _endTime;
    private TimeSpan _remainingOnPause;
    private DateTime _startedAt;
    private int _plannedSeconds;
    private int _completedSessions;
    private bool _nextIsLongBreak;

    [ObservableProperty] private string _selectedMode = "work";
    [ObservableProperty] private int _customMinutes = 15;
    [ObservableProperty] private string _remainingText = "25:00";
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _sessionCountText = "";

    public string WorkLabel => Loc.T("focus.work");
    public string ShortBreakLabel => Loc.T("focus.shortBreak");
    public string LongBreakLabel => Loc.T("focus.longBreak");
    public string CustomLabel => Loc.T("focus.custom");
    public string StopwatchLabel => Loc.T("focus.stopwatch");
    public string ResetLabel => Loc.T("focus.reset");
    public bool CustomVisible => SelectedMode == "custom";
    public string CustomMinutesLabel => Loc.T("focus.custom");

    public FocusViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Tick();
        UpdateSessionCount();
    }

    public void OnShown() => UpdateSessionCount();
    public void OnHidden() { }

    public string StartPauseLabel => !IsRunning && !IsPaused
        ? Loc.T("focus.start")
        : IsRunning ? Loc.T("focus.pause") : Loc.T("focus.resume");

    public string CurrentModeLabel => SelectedMode switch
    {
        "short" => ShortBreakLabel,
        "long" => LongBreakLabel,
        "custom" => CustomLabel,
        "stopwatch" => StopwatchLabel,
        _ => WorkLabel
    };

    partial void OnSelectedModeChanged(string value)
    {
        Stop(silent: true);
        OnPropertyChanged(nameof(CurrentModeLabel));
        OnPropertyChanged(nameof(CustomVisible));
    }

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StartPauseLabel));
    partial void OnIsPausedChanged(bool value) => OnPropertyChanged(nameof(StartPauseLabel));

    private int PlannedSecondsFor(string mode) => mode switch
    {
        "work" => AppServices.Settings.Focus.WorkMinutes * 60,
        "short" => AppServices.Settings.Focus.ShortBreakMinutes * 60,
        "long" => AppServices.Settings.Focus.LongBreakMinutes * 60,
        "custom" => Math.Max(1, CustomMinutes) * 60,
        _ => 0 // stopwatch
    };

    [RelayCommand]
    private void SwitchMode(string mode) => SelectedMode = mode;

    [RelayCommand]
    private void StartPause()
    {
        if (IsRunning) { Pause(); return; }
        if (IsPaused) { Resume(); return; }
        Start();
    }

    private void Start()
    {
        _plannedSeconds = PlannedSecondsFor(SelectedMode);
        _startedAt = DateTime.Now;
        _completedSessions = _repo.CountCompletedToday();
        if (SelectedMode == "stopwatch")
        {
            RemainingText = "0:00";
            ProgressFraction = 1;
        }
        else
        {
            _remainingOnPause = TimeSpan.FromSeconds(_plannedSeconds);
            _endTime = DateTime.Now.AddSeconds(_plannedSeconds);
        }
        IsRunning = true;
        IsPaused = false;
        _timer.Start();
    }

    private void Pause()
    {
        if (!IsRunning) return;
        if (SelectedMode != "stopwatch")
            _remainingOnPause = _endTime - DateTime.Now;
        IsRunning = false;
        IsPaused = true;
        _timer.Stop();
    }

    private void Resume()
    {
        if (!IsPaused) return;
        if (SelectedMode != "stopwatch")
            _endTime = DateTime.Now.Add(_remainingOnPause);
        IsRunning = true;
        IsPaused = false;
        _timer.Start();
    }

    [RelayCommand]
    private void Reset() => Stop(silent: true);

    private void Stop(bool silent)
    {
        if (IsRunning && SelectedMode != "stopwatch" && _startedAt != default)
        {
            var actual = (int)(DateTime.Now - _startedAt).TotalSeconds;
            if (actual > 5)
                _repo.Add(SelectedMode, _startedAt, DateTime.Now, _plannedSeconds, actual, completed: false);
        }
        _timer.Stop();
        IsRunning = false;
        IsPaused = false;
        _startedAt = default;
        var planned = PlannedSecondsFor(SelectedMode);
        RemainingText = SelectedMode == "stopwatch" ? "0:00" : FormatTime(TimeSpan.FromSeconds(planned));
        ProgressFraction = SelectedMode == "stopwatch" ? 1 : 0;
        UpdateSessionCount();
    }

    public void ToggleFromHotkey() => StartPause();

    private void Tick()
    {
        if (SelectedMode == "stopwatch")
        {
            var elapsed = DateTime.Now - _startedAt;
            RemainingText = FormatTime(elapsed);
            return;
        }

        var remaining = _endTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            Complete();
            return;
        }
        RemainingText = FormatTime(remaining);
        ProgressFraction = _plannedSeconds > 0
            ? Math.Clamp(1 - remaining.TotalSeconds / _plannedSeconds, 0, 1)
            : 0;
    }

    private void Complete()
    {
        _timer.Stop();
        IsRunning = false;
        IsPaused = false;
        RemainingText = "0:00";
        ProgressFraction = 1;

        var actual = (int)(DateTime.Now - _startedAt).TotalSeconds;
        _repo.Add(SelectedMode, _startedAt, DateTime.Now, _plannedSeconds, actual, completed: true);

        if (SelectedMode == "work")
        {
            _completedSessions++;
            _nextIsLongBreak = _completedSessions % Math.Max(1, AppServices.Settings.Focus.SessionsBeforeLongBreak) == 0;
            AppServices.Notify.ShowBalloon(Loc.T("notif.timerDone"), Loc.T("focus.completed"));
            AppServices.Notify.ShowIslandContext("\uE9D5", Loc.T("focus.completed"));
            SelectedMode = _nextIsLongBreak ? "long" : "short";
        }
        else
        {
            SelectedMode = "work";
        }
        UpdateSessionCount();
    }

    private void UpdateSessionCount() =>
        SessionCountText = $"{Loc.T("focus.sessions")}: {SafeCountToday()}";

    private int SafeCountToday()
    {
        try { return _repo.CountCompletedToday(); }
        catch { return 0; }
    }

    private static string FormatTime(TimeSpan t) =>
        t < TimeSpan.Zero ? "0:00" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
