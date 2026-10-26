// The portable shell uses WPF for UI and only System.Windows.Forms.NotifyIcon/dialogs.
// Explicit global aliases prevent Windows Forms implicit usings from shadowing UI types.
global using Brush = System.Windows.Media.Brush;
global using Button = System.Windows.Controls.Button;
global using TextBox = System.Windows.Controls.TextBox;
global using Point = System.Windows.Point;
global using Timer = System.Threading.Timer;
