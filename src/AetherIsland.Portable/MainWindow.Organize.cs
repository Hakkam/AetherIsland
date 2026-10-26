using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private readonly List<PortableTask> _tasks = PortableData.Read<List<PortableTask>>("tasks.json") ?? new();
    private readonly List<PortableNote> _notes = PortableData.Read<List<PortableNote>>("notes.json") ?? new();
    private readonly List<string> _shelf = PortableData.Read<List<string>>("shelf.json") ?? new();

    private UIElement RenderShelf()
    {
        var list = new StackPanel();
        var drop = new Border
        {
            Height = 55, CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(53, 65, 77)), BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(30, 36, 44)),
            AllowDrop = true, Margin = new Thickness(0, 7, 0, 5)
        };
        drop.Child = new TextBlock
        {
            Text = "Drop files here · paths saved locally",
            Foreground = Ui.Dim, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
            foreach (var p in paths)
                if ((File.Exists(p) || Directory.Exists(p)) && !_shelf.Contains(p, StringComparer.OrdinalIgnoreCase))
                    _shelf.Add(p);
            PortableData.Save("shelf.json", _shelf);
            Navigate("shelf");
        };
        var add = Ui.Button("Add files…", () =>
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog { Multiselect = true };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            foreach (var path in dialog.FileNames)
                if (!_shelf.Contains(path, StringComparer.OrdinalIgnoreCase)) _shelf.Add(path);
            PortableData.Save("shelf.json", _shelf); Navigate("shelf");
        });
        foreach (var path in _shelf.Take(10))
        {
            var row = Ui.H(Ui.Button("↗", () => OpenUrl(path), "Open file"),
                Ui.Button("↥", () => { }, "Drag this row to Windows Explorer"),
                Ui.Text(Path.GetFileName(path), 11),
                Ui.Button("×", () =>
                {
                    _shelf.Remove(path);
                    PortableData.Save("shelf.json", _shelf);
                    Navigate("shelf");
                }, "Remove reference"));
            row.Margin = new Thickness(0, 1, 0, 2);
            // Native DragDrop emits FileDrop paths, not a fake clipboard shortcut.
            row.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is Button) return;
                try
                {
                    var data = new DataObject(DataFormats.FileDrop, new[] { path });
                    DragDrop.DoDragDrop(row, data, DragDropEffects.Copy);
                }
                catch (Exception ex) { PortableData.Log("SHELF", ex.Message); }
            };
            list.Children.Add(row);
        }
        var section = Ui.V(Ui.H(Ui.Title("Files · Drag and drop"), add), drop, list);
        return Ui.WrapPage(section);
    }

    private UIElement RenderTasks()
    {
        var input = Ui.Input("", 345);
        var list = new StackPanel();
        var add = Ui.Button("+ Add task", () =>
        {
            var value = input.Text.Trim();
            if (value.Length == 0) return;
            _tasks.Add(new PortableTask { Title = value });
            PortableData.Save("tasks.json", _tasks);
            Navigate("tasks");
        });
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; } };
        foreach (var task in _tasks)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            var done = new CheckBox { IsChecked = task.Done, VerticalAlignment = VerticalAlignment.Center,
                Width = 22 };
            done.Checked += (_, _) => { task.Done = true; PortableData.Save("tasks.json", _tasks); };
            done.Unchecked += (_, _) => { task.Done = false; PortableData.Save("tasks.json", _tasks); };
            row.Children.Add(done);
            var title = Ui.Text(task.Title, 12);
            title.Width = 370;
            title.TextDecorations = task.Done ? TextDecorations.Strikethrough : null;
            row.Children.Add(title);
            row.Children.Add(Ui.Button("Delete", () =>
            {
                _tasks.Remove(task); PortableData.Save("tasks.json", _tasks); Navigate("tasks");
            }));
            list.Children.Add(row);
        }
        return Ui.WrapPage(Ui.V(Ui.Title("Tasks · Local and portable"),
            new Border { Height = 8 }, Ui.H(input, add), new Border { Height = 8 },
            list.Children.Count == 0 ? Ui.Text("No tasks yet", 11, false, Ui.Dim) : list));
    }

    private UIElement RenderNotes()
    {
        var editor = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Width = 450, Height = 96,
            Margin = new Thickness(0, 0, 9, 0) };
        var selected = _notes.OrderByDescending(n => n.UpdatedAt).FirstOrDefault();
        if (selected != null) editor.Text = selected.Content;
        var selector = new StackPanel { Width = 260 };
        void UpdateNotes() { PortableData.Save("notes.json", _notes); Navigate("notes"); }
        var save = Ui.Button("Save note", () =>
        {
            if (selected == null)
            {
                if (string.IsNullOrWhiteSpace(editor.Text)) return;
                selected = new PortableNote();
                _notes.Add(selected);
            }
            selected.Content = editor.Text;
            selected.UpdatedAt = DateTime.Now;
            UpdateNotes();
        });
        var add = Ui.Button("New note", () => { selected = null; editor.Text = ""; editor.Focus(); });
        var remove = Ui.Button("Delete note", () =>
        {
            if (selected == null) return;
            _notes.Remove(selected);
            UpdateNotes();
        });
        foreach (var note in _notes.OrderByDescending(n => n.UpdatedAt).Take(8))
        {
            var caption = note.Content.Split('\n').FirstOrDefault() ?? "";
            if (caption.Length > 36) caption = caption[..36] + "…";
            selector.Children.Add(Ui.Button(string.IsNullOrWhiteSpace(caption) ? "(empty note)" : caption,
                () => { selected = note; editor.Text = note.Content; }));
        }
        return Ui.WrapPage(Ui.V(Ui.Title("Notes · Saved in Data/notes.json"),
            new Border { Height = 7 }, Ui.H(editor, selector), new Border { Height = 6 },
            Ui.H(save, add, remove)));
    }

    private int _focusSeconds = 25 * 60;
    private readonly System.Windows.Threading.DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TextBlock? _focusText;
    private bool _focusHandlerAttached;
    private UIElement RenderFocus()
    {
        if (!_focusHandlerAttached)
        {
            _focusTimer.Tick += (_, _) =>
            {
                _focusSeconds = Math.Max(0, _focusSeconds - 1);
                if (_focusSeconds == 0) { _focusTimer.Stop(); _tray.ShowBalloonTip(3000, "Focus", "Timer finished", System.Windows.Forms.ToolTipIcon.Info); }
                if (_focusText != null) _focusText.Text = $"{_focusSeconds / 60:00}:{_focusSeconds % 60:00}";
            };
            _focusHandlerAttached = true;
        }
        _focusText = Ui.Text($"{_focusSeconds / 60:00}:{_focusSeconds % 60:00}", 34, true);
        return Ui.WrapPage(Ui.V(Ui.Title("Focus timer · 25 minutes"),
            new Border { Height = 7 }, _focusText,
            Ui.H(Ui.Button("Start / pause", () =>
            {
                if (_focusTimer.IsEnabled) _focusTimer.Stop();
                else if (_focusSeconds > 0) _focusTimer.Start();
            }), Ui.Button("Reset", () =>
            {
                _focusTimer.Stop(); _focusSeconds = 25 * 60;
                if (_focusText != null) _focusText.Text = "25:00";
            }))));
    }
}
