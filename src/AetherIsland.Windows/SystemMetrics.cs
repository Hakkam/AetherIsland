using System.Diagnostics;
using System.Runtime.InteropServices;
using AetherIsland.Windows.Interop;

namespace AetherIsland.Windows;

public sealed record SystemSample(
    double CpuPercent,
    double MemoryPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    double NetUpBps,
    double NetDownBps,
    bool HasBattery,
    byte BatteryPercent,
    bool Charging,
    uint BatteryLifeSeconds, // uint.MaxValue = unknown
    string? CpuTemperatureCelsius // null when sensors are not exposed
);

/// <summary>Lightweight periodic system sampling using official Win32 APIs.</summary>
public sealed class SystemMetricsSampler
{
    private (ulong idle, ulong kernel, ulong user) _lastCpu;
    private bool _cpuFirst = true;
    private Dictionary<uint, (ulong sent, ulong recv)> _lastNet = new();
    private bool _netFirst = true;
    private readonly Stopwatch _netClock = Stopwatch.StartNew();
    private DateTime _tempAttempt = DateTime.MinValue;
    private string? _lastTemp;

    public SystemSample Sample()
    {
        // CPU via GetSystemTimes deltas
        NativeMethods.GetSystemTimes(out var idleFt, out var kernelFt, out var userFt);
        var idle = ToUlong(idleFt);
        var kernel = ToUlong(kernelFt);
        var user = ToUlong(userFt);
        double cpu = 0;
        if (!_cpuFirst)
        {
            var dIdle = idle - _lastCpu.idle;
            var dKernel = kernel - _lastCpu.kernel;
            var dUser = user - _lastCpu.user;
            var total = dKernel + dUser;
            if (total > 0) cpu = 100.0 * (total - dIdle) / total;
        }
        _cpuFirst = false;
        _lastCpu = (idle, kernel, user);

        // Memory
        var mem = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        NativeMethods.GlobalMemoryStatusEx(ref mem);
        var memPct = mem.dwMemoryLoad;

        // Network throughput via cumulative interface counters
        double upBps = 0, downBps = 0;
        var elapsed = _netClock.Elapsed.TotalSeconds;
        if (elapsed > 0.2)
        {
            var now = ReadNetworkCounters();
            if (!_netFirst)
            {
                foreach (var (id, (sent, recv)) in now)
                {
                    if (_lastNet.TryGetValue(id, out var prev))
                    {
                        upBps += Math.Max(0, sent - prev.sent) / elapsed;
                        downBps += Math.Max(0, recv - prev.recv) / elapsed;
                    }
                }
            }
            _netFirst = false;
            _lastNet = now;
            _netClock.Restart();
        }

        // Battery
        var ps = new NativeMethods.SYSTEM_POWER_STATUS();
        NativeMethods.GetSystemPowerStatus(ref ps);
        var hasBattery = ps.BatteryFlag is not 128 and not 255;
        var lifeSeconds = ps.BatteryLifeTime == unchecked((uint)-1) ? uint.MaxValue : ps.BatteryLifeTime;

        return new SystemSample(
            Math.Round(cpu, 1),
            memPct,
            mem.ullTotalPhys - mem.ullAvailPhys,
            mem.ullTotalPhys,
            upBps, downBps,
            hasBattery,
            ps.BatteryLifePercent,
            ps.ACLineStatus == 1,
            lifeSeconds,
            TryReadCpuTemperature());
    }

    private static ulong ToUlong(NativeMethods.FILETIME ft) => ((ulong)ft.High << 32) | ft.Low;

    private static Dictionary<uint, (ulong sent, ulong recv)> ReadNetworkCounters()
    {
        var result = new Dictionary<uint, (ulong, ulong)>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                    or System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;
                var stats = nic.GetIPStatistics();
                result[(uint)nic.Id.GetHashCode()] = ((ulong)Math.Max(0, stats.BytesSent), (ulong)Math.Max(0, stats.BytesReceived));
            }
        }
        catch { }
        return result;
    }

    private string? TryReadCpuTemperature()
    {
        // Thermal zone WMI usually requires elevation and is absent on most consumer boards.
        // Try at most once per 2 minutes; return last known value otherwise.
        if ((DateTime.Now - _tempAttempt).TotalSeconds < 120) return _lastTemp;
        _tempAttempt = DateTime.Now;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var o in searcher.Get())
            {
                var raw = Convert.ToDouble(o["CurrentTemperature"]);
                _lastTemp = (raw / 10.0 - 273.15).ToString("0");
                return _lastTemp;
            }
        }
        catch
        {
            _lastTemp = null;
        }
        return _lastTemp;
    }
}

public sealed record DiskInfo(string Drive, ulong TotalBytes, ulong FreeBytes);

public static class PowerHelper
{
    public static void KeepAwake(bool enableDisplay)
    {
        var state = NativeMethods.EXECUTION_STATE.ES_CONTINUOUS | NativeMethods.EXECUTION_STATE.ES_SYSTEM_REQUIRED;
        if (enableDisplay) state |= NativeMethods.EXECUTION_STATE.ES_DISPLAY_REQUIRED;
        NativeMethods.SetThreadExecutionState(state);
    }

    public static void AllowSleep() => NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);

    public static void LockWorkstation() => NativeMethods.LockWorkStation();

    public static void SendMediaKey(byte vk)
    {
        NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}

public static class Disks
{
    public static List<DiskInfo> GetDisks()
    {
        var list = new List<DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                list.Add(new DiskInfo(drive.Name, (ulong)drive.TotalSize, (ulong)drive.AvailableFreeSpace));
            }
        }
        catch { }
        return list;
    }
}
