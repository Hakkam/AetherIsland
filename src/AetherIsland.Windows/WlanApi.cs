using System.Runtime.InteropServices;

namespace AetherIsland.Windows;

public sealed record WiFiNetwork(
    string Ssid,
    string ProfileName,
    int SignalPercent,
    bool Secured,
    bool Connected,
    bool Connectable);

public sealed record WiFiInterface(Guid Id, string Description, string State, WiFiNetwork? Current);

/// <summary>
/// Wi-Fi visibility via the official Native WiFi API (wlanapi.dll).
/// Viewing never requires administrator privileges. Connecting to open networks is
/// attempted; secured networks direct the user to Windows Settings.
/// </summary>
public static class WlanApi
{
    private const int WLAN_MAX_NAME_LENGTH = 256;

    public static List<WiFiInterface> EnumerateInterfaces()
    {
        var result = new List<WiFiInterface>();
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return result;
        try
        {
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr) != 0) return result;
            try
            {
                var count = Marshal.ReadInt32(listPtr);
                var first = Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo");
                var infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                for (var i = 0; i < count; i++)
                {
                    var p = IntPtr.Add(listPtr, (int)first + i * infoSize);
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(p);
                    var state = info.State switch
                    {
                        1 or 2 => "connected",
                        5 or 6 or 7 => "connecting",
                        _ => "disconnected"
                    };
                    result.Add(new WiFiInterface(info.InterfaceGuid, info.Description, state, null));
                }
            }
            finally { WlanFreeMemory(listPtr); }

            if (result.Count > 0)
            {
                var guid = result[0].Id;
                result[0] = result[0] with { Current = GetConnectedNetwork(handle, guid) };
            }
        }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
        return result;
    }

    public static List<WiFiNetwork> GetAvailableNetworks()
    {
        var networks = new List<WiFiNetwork>();
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return networks;
        try
        {
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr) != 0) return networks;
            WlanFreeMemory(listPtr);
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr2) != 0) return networks;
            try
            {
                var count = Marshal.ReadInt32(listPtr2);
                var first = Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo");
                var infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                if (count > 0)
                {
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(
                        IntPtr.Add(listPtr2, (int)first));
                    networks = GetNetworksForInterface(handle, info.InterfaceGuid);
                }
            }
            finally { WlanFreeMemory(listPtr2); }
        }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
        return networks;
    }

    private static WiFiNetwork? GetConnectedNetwork(IntPtr handle, Guid interfaceGuid)
    {
        var nets = GetNetworksForInterface(handle, interfaceGuid);
        return nets.FirstOrDefault(n => n.Connected);
    }

    private static List<WiFiNetwork> GetNetworksForInterface(IntPtr handle, Guid interfaceGuid)
    {
        var networks = new List<WiFiNetwork>();
        var g = interfaceGuid;
        if (WlanGetAvailableNetworkList(handle, ref g, 3, IntPtr.Zero, out var netListPtr) != 0)
            return networks;
        try
        {
            var count = Marshal.ReadInt32(netListPtr);
            var first = Marshal.OffsetOf<WLAN_AVAILABLE_NETWORK_LIST>("Network");
            var netSize = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
            for (var i = 0; i < count; i++)
            {
                var p = IntPtr.Add(netListPtr, (int)first + i * netSize);
                var net = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(p);
                var ssidLen = (int)net.uSsidLength;
                if (ssidLen is <= 0 or > 32) continue;
                var ssid = System.Text.Encoding.UTF8.GetString(net.Ssid, 0, ssidLen);
                if (string.IsNullOrWhiteSpace(ssid)) continue;
                networks.Add(new WiFiNetwork(
                    ssid,
                    net.ProfileName,
                    (int)net.SignalQuality,
                    net.SecurityEnabled != 0,
                    (net.Flags & 0x1) != 0,
                    net.Connectable != 0));
            }
        }
        finally { WlanFreeMemory(netListPtr); }

        return networks
            .GroupBy(n => n.Ssid)
            .Select(g => g.OrderByDescending(n => n.Connected).ThenByDescending(n => n.SignalPercent).First())
            .OrderByDescending(n => n.Connected)
            .ThenByDescending(n => n.SignalPercent)
            .ToList();
    }

    /// <summary>Connects to an open network by SSID. Returns an error message or null on success.</summary>
    public static string? ConnectOpen(string profileName)
    {
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return "Could not open WiFi service";
        try
        {
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr) != 0) return "No WiFi interface";
            try
            {
                var count = Marshal.ReadInt32(listPtr);
                var first = Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo");
                var infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                if (count == 0) return "No WiFi interface";
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(
                    IntPtr.Add(listPtr, (int)first));
                var guid = info.InterfaceGuid;

                var profilePtr = Marshal.StringToHGlobalUni(profileName);
                var ssid = new DOT11_SSID { uSsidLength = (uint)System.Text.Encoding.UTF8.GetBytes(profileName).Length, Ssid = new byte[32] };
                System.Text.Encoding.UTF8.GetBytes(profileName).CopyTo(ssid.Ssid, 0);
                var ssidPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DOT11_SSID>());
                Marshal.StructureToPtr(ssid, ssidPtr, false);
                try
                {
                    var parms = new WLAN_CONNECTION_PARAMETERS
                    {
                        ConnectionMode = 0, // wlan_connection_mode_profile
                        Profile = profilePtr,
                        Ssid = ssidPtr,
                        DesiredBssidList = IntPtr.Zero,
                        BssType = 1, // infrastructure
                        Flags = 0
                    };
                    var rc = WlanConnect(handle, ref guid, ref parms, IntPtr.Zero);
                    return rc == 0 ? null : $"Windows returned error {rc}";
                }
                finally
                {
                    Marshal.FreeHGlobal(profilePtr);
                    Marshal.FreeHGlobal(ssidPtr);
                }
            }
            finally { WlanFreeMemory(listPtr); }
        }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
    }

    public static void Disconnect()
    {
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return;
        try
        {
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr) != 0) return;
            try
            {
                var count = Marshal.ReadInt32(listPtr);
                var first = Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo");
                var infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                for (var i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(
                        IntPtr.Add(listPtr, (int)first + i * infoSize));
                    var g = info.InterfaceGuid;
                    WlanDisconnect(handle, ref g, IntPtr.Zero);
                }
            }
            finally { WlanFreeMemory(listPtr); }
        }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
    }

    // ---- interop ----

    [DllImport("wlanapi.dll")] private static extern int WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);
    [DllImport("wlanapi.dll")] private static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")] private static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr interfaceList);
    [DllImport("wlanapi.dll")] private static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid interfaceGuid, uint flags, IntPtr reserved, out IntPtr availableNetworkList);
    [DllImport("wlanapi.dll")] private static extern int WlanConnect(IntPtr handle, ref Guid interfaceGuid, ref WLAN_CONNECTION_PARAMETERS parameters, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern int WlanDisconnect(IntPtr handle, ref Guid interfaceGuid, IntPtr reserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO_LIST
    {
        public uint NumberOfItems;
        public uint Index;
        public WLAN_INTERFACE_INFO InterfaceInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        public uint State;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WLAN_MAX_NAME_LENGTH)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK_LIST
    {
        public uint NumberOfItems;
        public uint Index;
        public WLAN_AVAILABLE_NETWORK Network;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WLAN_MAX_NAME_LENGTH)]
        public string ProfileName;
        public uint uSsidLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Ssid;
        public uint BssType;
        public uint NumberOfBssIds;
        public int Connectable;
        public uint NotConnectableReason;
        public uint NumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] PhyTypes;
        public int MorePhyTypes;
        public uint SignalQuality;
        public int SecurityEnabled;
        public uint DefaultAuthAlgorithm;
        public uint DefaultCipherAlgorithm;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSsidLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Ssid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_CONNECTION_PARAMETERS
    {
        public uint ConnectionMode;
        public IntPtr Profile;
        public IntPtr Ssid;
        public IntPtr DesiredBssidList;
        public uint BssType;
        public uint Flags;
    }
}
