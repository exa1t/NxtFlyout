using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NxtFlyout.Services;

/// <summary>
/// Finds the icon of the app behind a media session, from the app id Windows reports
/// (for example "Spotify.exe", "Chrome" or a Store app's "Package!App" id). Results are cached.
/// </summary>
internal static class AppIconProvider
{
    private const int IconPixels = 32;

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the icon for <paramref name="appId"/>, or null if none can be found.</summary>
    public static ImageSource? GetIcon(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        if (Cache.TryGetValue(appId, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;
        try
        {
            icon = LoadIcon(appId);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException or ExternalException)
        {
            Log.Error($"Loading the icon for '{appId}' failed.", ex);
        }

        Cache[appId] = icon;
        return icon;
    }

    private static ImageSource? LoadIcon(string appId)
    {
        // Registered apps (Store apps, most browsers) are listed under the Apps folder by their id.
        var icon = FromParsingName(@"shell:AppsFolder\" + appId);
        if (icon is not null)
        {
            return icon;
        }

        // Classic desktop apps report their exe name; take the icon from the running process's file.
        var exePath = FindRunningExecutable(appId);
        return exePath is null ? null : FromParsingName(exePath);
    }

    private static string? FindRunningExecutable(string appId)
    {
        var name = appId;
        var bang = name.IndexOf('!');
        if (bang >= 0)
        {
            name = name[(bang + 1)..];
        }

        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Elevated or exiting process: try the next one.
                }
            }
        }

        return null;
    }

    private static ImageSource? FromParsingName(string parsingName)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
        {
            return null;
        }

        try
        {
            var size = new NativeSize { Width = IconPixels, Height = IconPixels };
            if (factory.GetImage(size, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out var bitmap) != 0 || bitmap == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return ToBitmapSource(bitmap);
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Copies the shell's 32-bit bitmap with its alpha channel intact
    /// (CreateBitmapSourceFromHBitmap would turn transparent areas black).
    /// </summary>
    private static BitmapSource ToBitmapSource(IntPtr bitmap)
    {
        if (GetObject(bitmap, Marshal.SizeOf<NativeBitmap>(), out var info) != 0
            && info.BitsPixel == 32
            && info.Bits != IntPtr.Zero)
        {
            var width = info.Width;
            var height = Math.Abs(info.Height);
            var stride = info.WidthBytes;
            var pixels = new byte[stride * height];
            Marshal.Copy(info.Bits, pixels, 0, pixels.Length);

            // A positive height means the rows are stored bottom-up.
            if (info.Height > 0)
            {
                var flipped = new byte[pixels.Length];
                for (var row = 0; row < height; row++)
                {
                    Buffer.BlockCopy(pixels, row * stride, flipped, (height - 1 - row) * stride, stride);
                }

                pixels = flipped;
            }

            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            source.Freeze();
            return source;
        }

        var fallback = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        fallback.Freeze();
        return fallback;
    }

    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_ICONONLY = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(IntPtr handle, int size, out NativeBitmap bitmap);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
