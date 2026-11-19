using System.Runtime.InteropServices;
using NxtFlyout.Core.Audio;

namespace NxtFlyout.Services;

/// <summary>
/// Reads and changes the master volume of the default playback device through the Windows
/// Core Audio API (IAudioEndpointVolume).
/// </summary>
/// <remarks>
/// The default device can change at any time (headphones plugged in), so the endpoint is
/// looked up for every call instead of being cached. Calls are cheap and only happen on key
/// presses and while the volume flyout is on screen.
/// </remarks>
internal sealed class CoreAudioVolumeService : IVolumeService
{
    private const int DataFlowRender = 0;
    private const int RoleConsole = 0;
    private const int ClsCtxAll = 23;

    private bool _failureLogged;

    public VolumeState? GetState()
    {
        VolumeState? state = null;

        Use(endpoint =>
        {
            Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var level));
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
            state = new VolumeState(level, muted);
        });

        return state;
    }

    public bool StepUp()
    {
        return Use(endpoint =>
        {
            var context = Guid.Empty;

            // Like the Windows volume keys: raising the volume also unmutes.
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
            if (muted)
            {
                Marshal.ThrowExceptionForHR(endpoint.SetMute(false, ref context));
            }

            Marshal.ThrowExceptionForHR(endpoint.VolumeStepUp(ref context));
        });
    }

    public bool StepDown()
    {
        return Use(endpoint =>
        {
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.VolumeStepDown(ref context));
        });
    }

    public bool ToggleMute()
    {
        return Use(endpoint =>
        {
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
            Marshal.ThrowExceptionForHR(endpoint.SetMute(!muted, ref context));
        });
    }

    /// <summary>Runs an action against the default playback device. Returns false if there is none or it failed.</summary>
    private bool Use(Action<IAudioEndpointVolume> action)
    {
        object? enumerator = null;
        IMMDevice? device = null;
        object? endpoint = null;

        try
        {
            // Not `new` on a [ComImport] coclass: NAudio (used for the spectrum capture) declares its
            // own coclass with the same CLSID, and the runtime maps a CLSID to only one managed class
            // per process. Once NAudio's is loaded, `new` here yields NAudio's RCW type and the cast
            // fails. CoCreateInstance hands back a plain RCW typed only by the interface we ask for.
            var classId = MMDeviceEnumeratorClassId;
            var enumeratorId = typeof(IMMDeviceEnumerator).GUID;
            var hr = CoCreateInstance(ref classId, IntPtr.Zero, ClsCtxAll, ref enumeratorId, out enumerator);
            if (hr != 0 || enumerator is null)
            {
                Marshal.ThrowExceptionForHR(hr);
                return false;
            }

            hr = ((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out device);
            if (hr != 0 || device is null)
            {
                // Typically "element not found": no playback device at the moment.
                return false;
            }

            var interfaceId = typeof(IAudioEndpointVolume).GUID;
            hr = device.Activate(ref interfaceId, ClsCtxAll, IntPtr.Zero, out endpoint);
            if (hr != 0 || endpoint is null)
            {
                return false;
            }

            action((IAudioEndpointVolume)endpoint);
            _failureLogged = false;
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidComObjectException or UnauthorizedAccessException)
        {
            // The flyout polls while it is visible; log a persistent failure once, not ten times a second.
            if (!_failureLogged)
            {
                _failureLogged = true;
                Log.Error("Accessing the system volume failed.", ex);
            }

            return false;
        }
        finally
        {
            Release(endpoint);
            Release(device);
            Release(enumerator);
        }
    }

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    // ----- Core Audio COM interfaces (only the members used here are named) -----

    private static readonly Guid MMDeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(
        ref Guid classId,
        IntPtr outer,
        int classContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
    }

    /// <summary>Method order must match the native vtable exactly; do not reorder.</summary>
    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float levelDb);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);

        [PreserveSig]
        int GetVolumeStepInfo(out uint step, out uint stepCount);

        [PreserveSig]
        int VolumeStepUp(ref Guid eventContext);

        [PreserveSig]
        int VolumeStepDown(ref Guid eventContext);
    }
}
