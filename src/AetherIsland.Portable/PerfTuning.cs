using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AetherIsland.Portable;

/// <summary>
/// Idle-friendly process policy: the island asks Windows to run it on the
/// efficiency cores (EcoQoS) at below-normal priority, and trims its working
/// set a few seconds after minimizing — so it never competes with real work.
/// </summary>
public static class PerfTuning
{
    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int infoClass,
        ref PROCESS_POWER_THROTTLING_STATE info, int size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool K32SetProcessWorkingSetSize(IntPtr hProcess, IntPtr min, IntPtr max);

    /// <summary>Call once at startup: efficiency-core hint + below-normal priority.</summary>
    public static void ApplyIdleFriendlyPolicy()
    {
        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = 1,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED
            };
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state,
                Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
        }
        catch { /* older Windows: skip silently */ }
        try
        {
            if (Process.GetCurrentProcess().PriorityClass != ProcessPriorityClass.BelowNormal)
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch { }
    }

    /// <summary>Release physical pages back to the OS. Called once after
    /// minimizing (never in a loop) — re-faults softly on next expand.</summary>
    public static void TrimWorkingSet()
    {
        try { K32SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1)); }
        catch { }
    }
}
