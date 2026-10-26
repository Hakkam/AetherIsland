using System.Runtime.InteropServices;
using AetherIsland.Windows.Interop;

namespace AetherIsland.Windows;

/// <summary>File-type icon lookup through the Windows shell (no thumbnail extraction, small overhead).</summary>
public static class ShellIcons
{
    /// <summary>Returns an HICON handle for a file path or extension. Caller must destroy it.</summary>
    public static IntPtr GetFileIconHandle(string path, bool large)
    {
        var flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_USEFILEATTRIBUTES | (large ? NativeMethods.SHGFI_LARGEICON : NativeMethods.SHGFI_SMALLICON);
        var attr = Directory.Exists(path)
            ? (uint)FileAttributes.Directory
            : (uint)FileAttributes.Normal;
        var info = new NativeMethods.SHFILEINFO();
        var res = NativeMethods.SHGetFileInfo(path, attr, ref info, (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(), flags);
        return res != IntPtr.Zero ? info.hIcon : IntPtr.Zero;
    }

    public static void DestroyIconHandle(IntPtr hIcon)
    {
        if (hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(hIcon);
    }
}
