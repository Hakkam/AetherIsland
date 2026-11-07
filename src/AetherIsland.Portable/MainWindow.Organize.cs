using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static readonly List<PortableTask> Tasks = PortableData.Read<List<PortableTask>>("tasks.json") ?? new();
    private static readonly List<PortableNote> Notes = PortableData.Read<List<PortableNote>>("notes.json") ?? new();
    private static readonly List<string> Shelf = PortableData.Read<List<string>>("shelf.json") ?? new();

    private static int _taskTab; // 0 active, 1 done, 2 trash

    // ---------------------------------------------------------------- shelf

    private static UIElement RenderShelf()
    {
        var stack = new StackPanel { Margin = new Thickness(14, 4, 12, 2) };

        var drop = new System.Windows.Controls.Border
        {
            Height = 74,
            CornerRadius = new CornerRadius(12),
            BorderBrush = Ui.Solid(Theme.IsDark ? "#39414F" : "#C6CEDA"),
            BorderThickness = new Thickness(1.2, 1.2, 1.2, 1.2),
            Background = Brushes.Transparent,
            AllowDrop = true,
            Margin = new Thickness(0, 2, 0, 6)
        };
        drop.Child = Ui.V(
            new ContentControl { Content = Iconography.Draw("drop", 20, 1.9, Theme.Dim), HorizontalAlignment = HorizontalAlignment.Center },
            Ui.Spacer(0, 4),
            Ui.Text("Drop files here", 12, true, Theme.Text, TextAlignment.Center),
            Ui.Text("Keep files handy, then drag them out anywhere", 9, false, Theme.Dim, TextAlignment.Center));
        drop.AllowDrop = true;
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
            foreach (var path in paths)
                if ((File.Exists(path) || Directory.Exists(path)) && !Shelf.Contains(path, StringComparer.OrdinalIgnoreCase))
                    Shelf.Add(path);
            PortableData.Save("shelf.json", Shelf);
            ReRenderIfVisible("shelf");
        };
        stack.Children.Add(drop);

        if (Shelf.Count == 0)
        {
            stack.Children.Add(Ui.Text("Nothing on the shelf yet.", 10, false, Theme.Dim, TextAlignment.Center));
        }
        foreach (var path in Shelf.Take(8))
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(AppIcons.Tile(path, 19));
            var name = Ui.Text(Path.GetFileName(path), 10.5);
            name.Margin = new Thickness(8, 0, 6, 0);
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var open = Iconography.IconButton("open", "Open", 24, () => External.Open(path), 12);
            Grid.SetColumn(open, 2);
            row.Children.Add(open);
            var remove = Iconography.IconButton("close", "Remove reference", 24, () =>
            {
                Shelf.Remove(path);
                PortableData.Save("shelf.json", Shelf);
                ReRenderIfVisible("shelf");
            }, 12);
            Grid.SetColumn(remove, 3);
            row.Children.Add(remove);

            // Drag the file back out to Explorer or any acceptor.
            row.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                try
                {
                    var data = new DataObject(DataFormats.FileDrop, new[] { path });
                    DragDrop.DoDragDrop(row, data, DragDropEffects.Copy);
                }
                catch (Exception ex) { PortableData.Log("SHELF", ex.Message); }
            };
            stack.Children.Add(row);
        }
        return Ui.WrapPage(stack);
    }

    // ---------------------------------------------------------------- tasks

    private static UIElement RenderTasks()
    {
        var stack = new StackPanel { Margin = new Thickness(12, 4, 12, 2) };

        // ---- header: title, color-coded status bar, filter ----
        var doneCount = Tasks.Count(t => t.Done && !t.Trashed);
        var overdueCount = Tasks.Count(t => !t.Done && !t.Trashed && t.DueAt is { } d && d < DateTime.Now);
        var activeCount = Tasks.Count(t => !t.Done && !t.Trashed) - overdueCount;
        var totalTasks = activeCount + overdueCount + doneCount;
        var header = new Grid { Margin = new Thickness(2, 0, 2, 7) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleBlock = Ui.H(Ui.Text("Tasks", 13, true), Ui.Spacer(9));
        StackPanel statusCounts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (totalTasks == 0)
            statusCounts.Children.Add(Ui.Text("no tasks yet", 8.5, false, Theme.Faint));
        else
        {
            statusCounts.Children.Add(Ui.Dot(Theme.Orange, activeCount + " active"));
            statusCounts.Children.Add(Ui.Spacer(7));
            if (overdueCount > 0)
            {
                statusCounts.Children.Add(Ui.Dot(Theme.Bad, overdueCount + " overdue"));
                statusCounts.Children.Add(Ui.Spacer(7));
            }
            statusCounts.Children.Add(Ui.Dot(Theme.Good, doneCount + " done"));
        }
        var progressCol = Ui.V(
            statusCounts,
            Ui.Spacer(0, 3),
            Ui.MultiBar(new (double, Brush)[]
            {
                (totalTasks == 0 ? 0 : activeCount / (double)totalTasks, Theme.Orange),
                (totalTasks == 0 ? 0 : overdueCount / (double)totalTasks, Theme.Bad),
                (totalTasks == 0 ? 0 : doneCount / (double)totalTasks, Theme.Good),
            }, 150));
        progressCol.VerticalAlignment = VerticalAlignment.Center;
        titleBlock.Children.Add(progressCol);
        header.Children.Add(titleBlock);
        var filterTabs = Ui.Segmented(new[] { "Active", "Done", "Trash" }, _taskTab, i => { _taskTab = i; ReRenderIfVisible("tasks"); });
        header.Children.Add(filterTabs);
        Grid.SetColumn(filterTabs, 2);
        stack.Children.Add(header);

        // ---- quick-add card with live reminder preview ----
        var addCard = new System.Windows.Controls.Border
        {
            Background = Theme.SectionCard,
            BorderBrush = Theme.CardEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(8, 7, 8, 6),
            Margin = new Thickness(0, 0, 0, 7)
        };
        var addBody = new StackPanel();
        var addRow = new Grid();
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var plusBtn = new System.Windows.Controls.Border
        {
            Width = 28, Height = 28, CornerRadius = new CornerRadius(9),
            Background = Theme.Accent, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Add task (Enter)"
        };
        plusBtn.Child = Iconography.Draw("plus", 14, 2.2, Ui.Solid("#0B0E12"));
        addRow.Children.Add(plusBtn);
        var input = Ui.Input("Add a task…  end with 15m, 2h or 14:30 for a reminder", double.NaN);
        input.HorizontalAlignment = HorizontalAlignment.Stretch;
        input.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(input, 1);
        addRow.Children.Add(input);
        var addBtn = Ui.TextButton("Add", () => AddTaskFromInput(input), "Add task (Enter)", 10,
            background: Theme.Chip, foreground: Theme.Text);
        Grid.SetColumn(addBtn, 2);
        addRow.Children.Add(addBtn);
        addBody.Children.Add(addRow);

        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 5, 0, 0) };
        var previewIcon = new ContentControl { Content = Iconography.Draw("clock", 11, 1.9, Theme.Faint), VerticalAlignment = VerticalAlignment.Center };
        var previewText = Ui.Text("", 8.8, true, Theme.Accent);
        previewRow.Children.Add(previewIcon);
        previewRow.Children.Add(Ui.Spacer(5));
        previewRow.Children.Add(previewText);
        addBody.Children.Add(previewRow);

        void UpdatePreview()
        {
            var raw = Ui.IsPlaceholder(input) ? "" : input.Text.Trim();
            var (_, due) = TaskReminders.Parse(raw);
            if (raw.Length == 0 || due is null)
            {
                previewText.Foreground = Theme.Faint;
                previewText.Text = raw.Length == 0
                    ? "Tip: end with 15m, 2h or 14:30 and the task will remind you"
                    : "No reminder scheduled — add 15m, 2h or 14:30 to the end";
                previewIcon.Content = Iconography.Draw("clock", 11, 1.9, Theme.Faint);
                return;
            }
            previewText.Foreground = Theme.Accent;
            var at = due.Value;
            previewText.Text = at > DateTime.Now
                ? "Will remind at " + at.ToString("HH:mm") + "  (in " + ((int)(at - DateTime.Now).TotalMinutes + 1) + " min)"
                : "Reminder time already passed — it will fire immediately";
            previewIcon.Content = Iconography.Draw("bell", 11, 1.9, Theme.Accent);
        }
        input.TextChanged += (_, _) => UpdatePreview();
        addCard.Child = addBody;
        stack.Children.Add(addCard);
        UpdatePreview();

        plusBtn.MouseLeftButtonDown += (_, _) => AddTaskFromInput(input);

        // ---- list ----
        var host = new ContentControl();
        stack.Children.Add(host);
        _currentTaskList = host;
        _currentTaskInput = input;
        host.Content = BuildTaskList();
        return Ui.WrapPage(stack);
    }

    private static ContentControl? _currentTaskList;
    private static TextBox? _currentTaskInput;

    private static void AddTaskFromInput(TextBox input)
    {
        var raw = Ui.IsPlaceholder(input) ? "" : input.Text.Trim();
        if (raw.Length == 0) return;
        var (title, due) = TaskReminders.Parse(raw);
        Tasks.Add(new PortableTask { Title = title, DueAt = due, CreatedAt = DateTime.Now });
        PortableData.Save("tasks.json", Tasks);
        _taskTab = 0;
        ReRenderIfVisible("tasks");
    }

    private static UIElement BuildTaskList()
    {
        var list = new StackPanel();
        var visible = _taskTab switch
        {
            0 => Tasks.Where(t => !t.Done && !t.Trashed).OrderBy(t => t.DueAt ?? DateTime.MaxValue).ToList(),
            1 => Tasks.Where(t => t.Done && !t.Trashed).OrderByDescending(t => t.DoneAt ?? DateTime.MinValue).ToList(),
            _ => Tasks.Where(t => t.Trashed).OrderByDescending(t => t.CreatedAt).ToList(),
        };
        if (visible.Count == 0)
        {
            var empty = Ui.V(
                new ContentControl { Content = Iconography.Draw(_taskTab == 0 ? "check" : "trash", 20, 1.9, Theme.Faint), HorizontalAlignment = HorizontalAlignment.Center },
                Ui.Spacer(0, 6),
                Ui.Text(
                    _taskTab == 0 ? "All clear — nothing to do. Add your first task above." :
                    _taskTab == 1 ? "No completed tasks yet — finish one and it lands here." : "Trash is empty.",
                    10, false, Theme.Dim, TextAlignment.Center));
            empty.Margin = new Thickness(0, 12, 0, 6);
            list.Children.Add(empty);
            return list;
        }
        foreach (var task in visible)
        {
            var captured = task;
            var card = new System.Windows.Controls.Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 3),
                Background = Brushes.Transparent
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var isOverdue = task.DueAt is { } dd && dd < DateTime.Now && !task.Done;
            var check = new Ui.Check(task.Done, isOverdue, on =>
            {
                captured.Done = on;
                captured.DoneAt = on ? DateTime.Now : null;
                PortableData.Save("tasks.json", Tasks);
                ReRenderIfVisible("tasks");
            }, task.Done ? "Done — click to reopen" : isOverdue ? "Overdue — click to mark done" : "Mark as done")
            { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var textCol = Ui.V(Ui.Text(captured.Title, 10.5, captured.Done, captured.Done ? Theme.Dim : Theme.Text, wrap: true));
            if (captured.DueAt is { } due)
            {
                var overdue = due < DateTime.Now && !captured.Done;
                textCol.Children.Add(Ui.Text(
                    overdue ? "overdue · was " + due.ToString("HH:mm") : "reminds at " + due.ToString("HH:mm"),
                    8.3, false, overdue ? Theme.Bad : Theme.Accent));
            }
            textCol.Margin = new Thickness(6, 0, 6, 0);
            textCol.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(textCol, 1);
            row.Children.Add(textCol);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (_taskTab == 1)
            {
                actions.Children.Add(Ui.TextButton("Reopen", () =>
                {
                    captured.Done = false;
                    captured.DoneAt = null;
                    PortableData.Save("tasks.json", Tasks);
                    ReRenderIfVisible("tasks");
                }, "Back to active", 8.5, background: Brushes.Transparent, foreground: Theme.Dim));
            }
            if (_taskTab == 2)
            {
                actions.Children.Add(Ui.TextButton("Restore", () =>
                {
                    captured.Trashed = false;
                    PortableData.Save("tasks.json", Tasks);
                    ReRenderIfVisible("tasks");
                }, "Restore task", 8.5, background: Brushes.Transparent, foreground: Theme.Text));
            }
            actions.Children.Add(Iconography.IconButton("trash", _taskTab == 2 ? "Delete forever" : "Move to trash", 24, () =>
            {
                if (_taskTab == 2) Tasks.Remove(captured);
                else captured.Trashed = true;
                PortableData.Save("tasks.json", Tasks);
                ReRenderIfVisible("tasks");
            }, 12));
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);

            card.Child = row;
            // hover: the row lights up like the reference's interactive surfaces
            card.MouseEnter += (_, _) => card.Background = Theme.Chip;
            card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
            list.Children.Add(card);
        }
        return list;
    }

    // ---------------------------------------------------------------- notes

    private static PortableNote? _selectedNote;
    private static TextBox? _noteEditor;

    private static UIElement RenderNotes()
    {
        var grid = new Grid { Margin = new Thickness(12, 4, 10, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });

        _selectedNote ??= Notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.UpdatedAt).FirstOrDefault();

        var editorCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 118,
            Background = Theme.Chip,
            Foreground = Theme.Text,
            BorderBrush = Theme.CardEdge,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 7, 9, 7),
            FontSize = 11.5
        };
        editor.Template = Ui.TextBoxTemplate();
        if (_selectedNote != null) editor.Text = _selectedNote.Content;
        _noteEditor = editor;

        // Autosave with a short debounce; switching modules or closing keeps text.
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            if (_selectedNote == null)
            {
                if (string.IsNullOrWhiteSpace(editor.Text)) return;
                _selectedNote = new PortableNote();
                Notes.Add(_selectedNote);
            }
            if (_selectedNote.Content != editor.Text)
            {
                _selectedNote.Content = editor.Text;
                _selectedNote.UpdatedAt = DateTime.Now;
                PortableData.Save("notes.json", Notes);
            }
        };
        editor.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };

        editorCol.Children.Add(editor);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(Ui.TextButton("New note", () =>
        {
            _selectedNote = null;
            editor.Text = "";
            editor.Focus();
        }, "Create a fresh note", 9.5, background: Theme.Chip, foreground: Theme.Text));
        actions.Children.Add(Ui.TextButton("Pin", () =>
        {
            if (_selectedNote == null) return;
            _selectedNote.Pinned = !_selectedNote.Pinned;
            _selectedNote.UpdatedAt = DateTime.Now;
            PortableData.Save("notes.json", Notes);
            ReRenderIfVisible("notes");
        }, "Pin to top", 9.5, background: Theme.Chip, foreground: Theme.Text));
        actions.Children.Add(Ui.TextButton("Copy", () =>
        {
            if (_selectedNote != null) System.Windows.Clipboard.SetText(string.IsNullOrEmpty(editor.Text) ? "" : editor.Text);
        }, "Copy note text", 9.5, background: Theme.Chip, foreground: Theme.Text));
        actions.Children.Add(Ui.TextButton("Delete", () =>
        {
            if (_selectedNote == null) return;
            Notes.Remove(_selectedNote);
            PortableData.Save("notes.json", Notes);
            _selectedNote = Notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.UpdatedAt).FirstOrDefault();
            ReRenderIfVisible("notes");
        }, "Delete note", 9.5, background: Theme.Chip, foreground: Theme.Bad));
        editorCol.Children.Add(actions);
        var hint = Ui.Text("The first line becomes the title. Notes autosave locally.", 8.8, false, Theme.Faint);
        hint.Margin = new Thickness(2, 6, 0, 0);
        editorCol.Children.Add(hint);
        grid.Children.Add(editorCol);

        var listCol = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        listCol.Children.Add(Ui.SectionTitle("notes", "Notes"));
        listCol.Children.Add(Ui.Spacer(0, 5));
        foreach (var note in Notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.UpdatedAt).Take(6))
        {
            var captured = note;
            var caption = note.Content.Split('\n').FirstOrDefault() ?? "";
            if (caption.Length > 26) caption = caption[..26] + "…";
            var row = new System.Windows.Controls.Border
            {
                Background = ReferenceEquals(captured, _selectedNote) ? Theme.Chip : Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var rowStack = Ui.V(
                Ui.Text((captured.Pinned ? "● " : "") + (string.IsNullOrWhiteSpace(caption) ? "(empty note)" : caption), 9.8, ReferenceEquals(captured, _selectedNote)),
                Ui.Text(captured.UpdatedAt.ToString("MMM d HH:mm"), 8, false, Theme.Faint));
            row.Child = rowStack;
            row.MouseLeftButtonDown += (_, _) =>
            {
                _selectedNote = captured;
                ReRenderIfVisible("notes");
            };
            listCol.Children.Add(row);
        }
        if (Notes.Count == 0)
            listCol.Children.Add(Ui.Text("No notes yet.", 9.5, false, Theme.Dim));
        grid.Children.Add(listCol);
        Grid.SetColumn(listCol, 1);
        return Ui.WrapPage(grid);
    }
}

/// <summary>Parses the reference's task suffixes: "… 15m", "… 2h", "… 14:30".</summary>
internal static class TaskReminders
{
    public static (string title, DateTime? due) Parse(string raw)
    {
        var words = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            var last = words[^1].ToLowerInvariant();
            var now = DateTime.Now;
            if (last.EndsWith('m') && last.Length <= 5 && int.TryParse(last[..^1], out var minutes) && minutes is > 0 and <= 720)
                return (string.Join(' ', words[..^1]), now.AddMinutes(minutes));
            if (last.EndsWith('h') && last.Length <= 4 && int.TryParse(last[..^1], out var hours) && hours is > 0 and <= 48)
                return (string.Join(' ', words[..^1]), now.AddHours(hours));
            if (TimeSpan.TryParseExact(last, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var time))
            {
                var due = now.Date + time;
                if (due <= now) due = due.AddDays(1);
                return (string.Join(' ', words[..^1]), due);
            }
        }
        return (raw, null);
    }
}
