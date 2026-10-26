// The portable shell uses WPF for UI and only System.Windows.Forms.NotifyIcon/dialogs.
// Explicit global aliases prevent Windows Forms implicit usings from shadowing UI types.
global using Brush = System.Windows.Media.Brush;
global using Button = System.Windows.Controls.Button;
global using TextBox = System.Windows.Controls.TextBox;
global using Point = System.Windows.Point;
global using Timer = System.Threading.Timer;
global using Color = System.Windows.Media.Color;
global using Brushes = System.Windows.Media.Brushes;
global using FontFamily = System.Windows.Media.FontFamily;
global using Orientation = System.Windows.Controls.Orientation;
global using CheckBox = System.Windows.Controls.CheckBox;
global using DataFormats = System.Windows.DataFormats;
global using DataObject = System.Windows.DataObject;
global using DragDrop = System.Windows.DragDrop;
global using DragDropEffects = System.Windows.DragDropEffects;
global using Size = System.Windows.Size;
global using MessageBox = System.Windows.MessageBox;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using VerticalAlignment = System.Windows.VerticalAlignment;
