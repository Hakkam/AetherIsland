using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AetherIsland.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static int _networkTab; // 0 = networks, 1 = devices

    private static UIElement RenderNetwork()
    {
        var stack = new StackPanel { Margin = new Thickness(12, 4, 10, 2) };

        // ---- header: radio states + tab switch ----
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var radios = new StackPanel { Orientation = Orientation.Horizontal };
        var wifiLabel = Ui.Text("Wi-Fi  ", 11.5, true);
        var wifiState = Ui.Text("…", 9.5, false, Theme.Dim);
        var btLabel = Ui.Text("     Bluetooth  ", 11.5, true);
        var btState = Ui.Text("…", 9.5, false, Theme.Dim);
        radios.Children.Add(Iconography.Draw("wifi", 15, 2, Theme.Accent));
        radios.Children.Add(wifiLabel);
        radios.Children.Add(wifiState);
        radios.Children.Add(Iconography.Draw("bluetooth", 14, 2, Theme.Accent));
        radios.Children.Add(btLabel);
        radios.Children.Add(btState);
        header.Children.Add(radios);
        var tabs = Ui.Segmented(new[] { "Networks", "Devices" }, _networkTab, i => { _networkTab = i; ReRenderIfVisible("network"); });
        header.Children.Add(tabs);
        Grid.SetColumn(tabs, 1);
        stack.Children.Add(header);
        stack.Children.Add(Ui.Spacer(0, 6));

        var listHost = new ContentControl();
        stack.Children.Add(listHost);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var connected = Ui.Text("", 9.5, false, Theme.Dim);
        footer.Children.Add(connected);
        footer.Children.Add(Ui.Spacer(10));
        footer.Children.Add(Ui.TextButton("Refresh", () => ReRenderIfVisible("network"), "Rescan", 9, background: Theme.Chip, foreground: Theme.Text));
        footer.Children.Add(Ui.TextButton("Wi-Fi settings", () => External.Open("ms-settings:network-wifi"), "Open Windows settings", 9, background: Theme.Chip, foreground: Theme.Text));
        stack.Children.Add(footer);

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var (wifiAvail, wifiOn) = Toggles.GetRadioState("wifi");
            var (btAvail, btOn) = Toggles.GetRadioState("bluetooth");
            List<WiFiNetwork> networks = new();
            string? connectError = null;
            try { networks = WlanApi.GetAvailableNetworks(); }
            catch (Exception ex) { connectError = ex.Message; }
            List<(string name, bool connected)> devices = new();
            try
            {
                var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var found = await DeviceInformation.FindAllAsync(selector,
                    new[] { "System.Devices.Aep.IsConnected" });
                foreach (var info in found)
                {
                    var isConnected = info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value) && value is true;
                    devices.Add((string.IsNullOrEmpty(info.Name) ? "Unknown device" : info.Name, isConnected));
                }
            }
            catch (Exception ex) { PortableData.Log("BLUETOOTH", ex.Message); }

            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                wifiState.Text = !wifiAvail ? "unavailable" : wifiOn ? "On" : "Off";
                btState.Text = !btAvail ? "unavailable" : btOn ? "On" : "Off";
                wifiLabel.Foreground = wifiOn ? Theme.Text : Theme.Dim;
                btLabel.Foreground = btOn ? Theme.Text : Theme.Dim;

                var pane = new StackPanel();
                if (_networkTab == 0)
                {
                    if (connectError != null)
                        pane.Children.Add(Ui.Text("Wireless adapter unavailable: " + connectError, 10.5, false, Theme.Dim));
                    else if (networks.Count == 0)
                        pane.Children.Add(Ui.Text(wifiOn ? "No networks found — try Refresh." : "Wi-Fi is off.", 10.5, false, Theme.Dim));
                    var columns = new StackPanel { Orientation = Orientation.Horizontal };
                    var leftCol = new StackPanel { Width = 420 };
                    var rightCol = new StackPanel { Width = 420 };
                    columns.Children.Add(leftCol);
                    columns.Children.Add(rightCol);
                    for (var i = 0; i < Math.Min(networks.Count, 10); i++)
                    {
                        var net = networks[i];
                        var row = new Grid { Margin = new Thickness(2, 4, 2, 4) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.Children.Add(Iconography.Draw("wifi", 13, 1.9, net.Connected ? Theme.Accent : Theme.Dim));
                        var nameCol = Ui.V(
                            Ui.Text(net.Ssid, 10.5, net.Connected),
                            Ui.Text($"{(net.Secured ? "Secured" : "Open")} · signal {net.SignalPercent}%", 8.8, false, Theme.Dim));
                        nameCol.Margin = new Thickness(7, 0, 6, 0);
                        Grid.SetColumn(nameCol, 1);
                        row.Children.Add(nameCol);
                        if (net.Connected)
                        {
                            var disconnect = Ui.TextButton("Disconnect", () =>
                            {
                                try { WlanApi.Disconnect(); }
                                catch (Exception ex) { PortableData.Log("WLAN", ex.Message); }
                                ReRenderIfVisible("network");
                            }, "Disconnect this network", 9, background: Theme.Chip, foreground: Theme.Text);
                            Grid.SetColumn(disconnect, 2);
                            row.Children.Add(disconnect);
                        }
                        else
                        {
                            var connect = Ui.TextButton("Connect", () =>
                            {
                                var result = WlanApi.ConnectOpen(net.ProfileName is { Length: > 0 } ? net.ProfileName : net.Ssid);
                                if (result is { Length: > 0 })
                                    App.NotifyBalloon("Wi-Fi", "Windows could not connect: saved profile required. Open Wi-Fi settings to enter the password.");
                                ReRenderIfVisible("network");
                            }, net.ProfileName is { Length: > 0 } ? "Connect using the saved profile" : "Open Windows settings (password needed)", 9,
                                background: Theme.Accent, foreground: Ui.Solid("#0B0E12"));
                            Grid.SetColumn(connect, 2);
                            row.Children.Add(connect);
                        }
                        (i % 2 == 0 ? leftCol : rightCol).Children.Add(row);
                    }
                    pane.Children.Add(columns);
                    var currentNetwork = networks.FirstOrDefault(n => n.Connected);
                    connected.Text = currentNetwork is null ? "Not connected" : $"Connected to {currentNetwork.Ssid}";
                }
                else
                {
                    if (devices.Count == 0)
                        pane.Children.Add(Ui.Text(btAvail ? "No paired Bluetooth devices." : "Bluetooth radio unavailable.", 10.5, false, Theme.Dim));
                    foreach (var (name, isConnected) in devices.Take(8))
                    {
                        var row = new Grid { Margin = new Thickness(2, 4, 2, 4) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.Children.Add(Iconography.Draw("bluetooth", 12, 1.9, isConnected ? Theme.Accent : Theme.Dim));
                        var nameCol = Ui.V(
                            Ui.Text(name, 10.5),
                            Ui.Text(isConnected ? "Connected" : "Paired", 8.8, false, Theme.Dim));
                        nameCol.Margin = new Thickness(7, 0, 6, 0);
                        Grid.SetColumn(nameCol, 1);
                        row.Children.Add(nameCol);
                        row.Children.Add(Ui.TextButton("Manage", () => External.Open("ms-settings:bluetooth"),
                            "Pairing and connection management lives in Windows settings", 9,
                            background: Theme.Chip, foreground: Theme.Text));
                        pane.Children.Add(row);
                    }
                    connected.Text = devices.Count(d => d.connected) + " connected device(s)";
                }
                listHost.Content = pane;
            });
        });

        return Ui.WrapPage(stack);
    }
}
