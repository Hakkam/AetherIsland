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
        var stack = new StackPanel { Margin = new Thickness(14, 4, 12, 2) };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = Ui.Input("Add a task… end with 15m, 2h or 14:30 for a reminder", 420);
        var add = Ui.TextButton("+ Add task", () =>
        {
            var raw = Ui.IsPlaceholder(input) ? "" : input.Text.Trim();
            if (raw.Length == 0) return;
            var (title, due) = TaskReminders.Parse(raw);
            Tasks.Add(new PortableTask { Title = title, DueAt = due });
            PortableData.Save("tasks.json", Tasks);
            ReRenderIfVisible("tasks");
        });
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; } };
        header.Children.Add(input);
        header.Children.Add(add);
        Grid.SetColumn(add, 1);
        stack.Children.Add(header);
        stack.Children.Add(Ui.Spacer(0, 7));

        var tabsRow = new Grid();
        tabsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tabsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var counts = Ui.Text($"{Tasks.Count(t => !t.Done && !t.Trashed)} open · {Tasks.Count(t => t.Done && !t.Trashed)} done", 9, false, Theme.Faint);
        tabsRow.Children.Add(counts);
        tabsRow.Children.Add(Ui.Segmented(new[] { "Active", "Done", "Trash" }, _taskTab, i => { _taskTab = i; ReRenderIfVisible("tasks"); }));
        Grid.SetColumn(tabsRow.Children[1], 1);
        stack.Children.Add(tabsRow);
        stack.Children.Add(Ui.Spacer(0, 4));

        var host = new ContentControl();
        stack.Children.Add(host);
        _currentTaskList = host;
        _currentTaskInput = input;
        host.Content = BuildTaskList(input);
        return Ui.WrapPage(stack);
    }

    private static ContentControl? _currentTaskList;
    private static TextBox? _currentTaskInput;

    private static UIElement BuildTaskList(TextBox input)
    {
        var list = new StackPanel();
        var visible = _taskTab switch
        {
            0 => Tasks.Where(t => !t.Done && !t.Trashed).ToList(),
            1 => Tasks.Where(t => t.Done && !t.Trashed).ToList(),
            _ => Tasks.Where(t => t.Trashed).ToList(),
        };
        if (visible.Count == 0)
        {
            list.Children.Add(Ui.Text(
                _taskTab == 0 ? "Nothing to do. Add a task above." :
                _taskTab == 1 ? "No completed tasks yet." : "Trash is empty.",
                10.5, false, Theme.Dim, TextAlignment.Center));
            list.Margin = new Thickness(0, 14, 0, 6);
            return list;
        }
        foreach (var task in visible)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var done = new CheckBox
            {
                IsChecked = task.Done,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            done.Checked += (_, _) =>
            {
                task.Done = true;
                task.DoneAt = DateTime.Now;
                PortableData.Save("tasks.json", Tasks);
                ReRenderIfVisible("tasks");
            };
            done.Unchecked += (_, _) =>
            {
                task.Done = false;
                task.DoneAt = null;
                PortableData.Save("tasks.json", Tasks);
                ReRenderIfVisible("tasks");
            };
            row.Children.Add(done);
            var titleCol = Ui.V(Ui.Text(task.Title, 11, task.Done, Theme.Text, wrap: true));
            if (task.DueAt is { } due)
                titleCol.Children.Add(Ui.Text(due >= DateTime.Now ? $"Due {due:HH:mm}" : $"Overdue · {due:HH:mm}", 8.5, false,
                    due >= DateTime.Now ? Theme.Dim : Theme.Bad));
            titleCol.Margin = new Thickness(4, 0, 6, 0);
            Grid.SetColumn(titleCol, 1);
            row.Children.Add(titleCol);
            if (_taskTab == 2)
            {
                var restore = Ui.TextButton("Restore", () =>
                {
                    task.Trashed = false;
                    PortableData.Save("tasks.json", Tasks);
                    ReRenderIfVisible("tasks");
                }, "Restore task", 8.5, background: Theme.Chip, foreground: Theme.Text);
                Grid.SetColumn(restore, 2);
                row.Children.Add(restore);
            }
            var trash = Iconography.IconButton("trash", _taskTab == 2 ? "Delete forever" : "Move to trash", 24, () =>
            {
                if (_taskTab == 2) Tasks.Remove(task);
                else task.Trashed = true;
                PortableData.Save("tasks.json", Tasks);
                ReRenderIfVisible("tasks");
            }, 12);
            Grid.SetColumn(trash, 3);
            row.Children.Add(trash);
            list.Children.Add(row);
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
