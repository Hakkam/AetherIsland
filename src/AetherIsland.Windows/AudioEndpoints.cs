using System.Runtime.InteropServices;

namespace AetherIsland.Windows;

public sealed record AudioDeviceState(bool Available, bool Muted, double VolumePercent);

/// <summary>
/// Controls the default render (speaker) and capture (microphone) endpoints through
/// the official Windows Core Audio IAudioEndpointVolume interface. No elevation required.
/// </summary>
public static class AudioEndpoints
{
    private static readonly object Lock = new();

    public static AudioDeviceState GetSpeaker() => Get(EDataFlow.Render);
    public static AudioDeviceState GetMicrophone() => Get(EDataFlow.Capture);

    public static void SetSpeakerMute(bool mute) => SetMute(EDataFlow.Render, mute);
    public static void SetMicrophoneMute(bool mute) => SetMute(EDataFlow.Capture, mute);

    public static bool ToggleSpeakerMute() => ToggleMute(EDataFlow.Render);
    public static bool ToggleMicrophoneMute() => ToggleMute(EDataFlow.Capture);

    private static AudioDeviceState Get(EDataFlow flow)
    {
        lock (Lock)
        {
            try
            {
                using var ep = GetEndpoint(flow);
                if (ep is null) return new AudioDeviceState(false, false, 0);
                var vol = AudioInterop.ActivateVolume(ep);
                using (vol)
                {
                    vol.GetMute(out var muted);
                    vol.GetMasterVolumeLevelScalar(out var level);
                    return new AudioDeviceState(true, muted, Math.Round(level * 100));
                }
            }
            catch
            {
                return new AudioDeviceState(false, false, 0);
            }
        }
    }

    private static void SetMute(EDataFlow flow, bool mute)
    {
        lock (Lock)
        {
            try
            {
                using var ep = GetEndpoint(flow);
                if (ep is null) return;
                var vol = AudioInterop.ActivateVolume(ep);
                using (vol) vol.SetMute(mute, Guid.Empty);
            }
            catch { }
        }
    }

    private static bool ToggleMute(EDataFlow flow)
    {
        lock (Lock)
        {
            try
            {
                using var ep = GetEndpoint(flow);
                if (ep is null) return false;
                var vol = AudioInterop.ActivateVolume(ep);
                using (vol)
                {
                    vol.GetMute(out var muted);
                    vol.SetMute(!muted, Guid.Empty);
                    return !muted;
                }
            }
            catch { return false; }
        }
    }

    private static IMMDevice? GetEndpoint(EDataFlow flow)
    {
        var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"); // MMDeviceEnumerator
        var type = Type.GetTypeFromCLSID(clsid);
        if (type is null) return null;
        var enumerator = (IMMDeviceEnumerator)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());
        try
        {
            enumerator.GetDefaultAudioEndpoint(flow, ERole.EMultimedia, out var device);
            return device;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }
}

internal enum EDataFlow { Render = 0, Capture = 1 }
internal enum ERole { EConsole = 0, EMultimedia = 1, ECommunications = 2 }
internal enum CLSCTX { CLSCTX_ALL = 23 }

internal static class AudioInterop
{
    internal static IAudioEndpointVolume ActivateVolume(IMMDevice device)
    {
        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out var iface);
        return (IAudioEndpointVolume)iface;
    }
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    // rest of vtable not needed
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int Item(int index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice : IDisposable
{
    [PreserveSig] int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    void OpenPropertyStore(int access, out object store);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out int state);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume : IDisposable
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint channelCount);
    [PreserveSig] int SetMasterVolumeLevel(float levelDb, Guid context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid context);
    [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, Guid context);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid context);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute(bool mute, Guid context);
    [PreserveSig] int GetMute(out bool mute);
    [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
    [PreserveSig] int VolumeStepUp(Guid context);
    [PreserveSig] int VolumeStepDown(Guid context);
    [PreserveSig] int QueryHardwareSupport(out uint support);
    [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
}
