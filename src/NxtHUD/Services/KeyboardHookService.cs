using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using NxtHUD.Core.Input;
using NxtHUD.Core.Media;
using NxtHUD.Interop;

namespace NxtHUD.Services;

/// <summary>
/// Watches for media, volume and lock keys system-wide with a low-level keyboard hook
/// (WH_KEYBOARD_LL).
/// </summary>
/// <remarks>
/// <para>Windows calls the hook on the thread that installed it (the UI thread) and expects it
/// to return quickly, so the callback does nothing but queue events.</para>
/// <para>Media and lock keys are only observed and always passed on. Volume keys are passed on
/// too unless <see cref="SuppressVolumeKeys"/> is set, in which case the app is expected to
/// change the volume itself; that is what keeps the built-in Windows volume overlay away.</para>
/// <para>A hook adds a little work to every keystroke on the system, so it is installed only
/// while a feature that needs it is switched on.</para>
/// </remarks>
internal sealed class KeyboardHookService : IDisposable
{
    private static readonly IntPtr Handled = new(1);

    private readonly Dispatcher _dispatcher;

    // Held in a field so the garbage collector cannot collect the delegate while Windows still calls it.
    private readonly NativeMethods.LowLevelKeyboardProc _callback;

    /// <summary>Lock keys currently held down, so auto-repeat does not announce the key again.</summary>
    private readonly HashSet<int> _heldLockKeys = new();

    private IntPtr _hook;

    public KeyboardHookService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _callback = OnKeyboardEvent;
    }

    /// <summary>Raised on the UI thread, shortly after a media key goes down.</summary>
    public event EventHandler<MediaKey>? MediaKeyPressed;

    /// <summary>Raised on the UI thread for every volume key press, including auto-repeat.</summary>
    public event EventHandler<VolumeKey>? VolumeKeyPressed;

    /// <summary>Raised on the UI thread once per physical press of a lock key.</summary>
    public event EventHandler<LockKey>? LockKeyPressed;

    /// <summary>
    /// When true, volume keys are swallowed: Windows and other apps never see them, and the
    /// handler of <see cref="VolumeKeyPressed"/> must change the volume itself.
    /// </summary>
    public bool SuppressVolumeKeys { get; set; }

    public bool IsRunning => _hook != IntPtr.Zero;

    /// <summary>Installs the hook. Must be called on the UI thread.</summary>
    /// <exception cref="Win32Exception">Windows refused to install the hook.</exception>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _heldLockKeys.Clear();
    }

    public void Dispose() => Stop();

    private IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && HandleKey(wParam.ToInt64(), lParam))
        {
            return Handled;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>Returns true when the key must not be passed on.</summary>
    private bool HandleKey(long message, IntPtr lParam)
    {
        var isDown = message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN;
        var isUp = message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP;
        if (!isDown && !isUp)
        {
            return false;
        }

        // lParam points to a KBDLLHOOKSTRUCT, whose first field is the virtual-key code.
        var virtualKey = Marshal.ReadInt32(lParam);

        if (SystemKeyMap.VolumeFromVirtualKey(virtualKey) is VolumeKey volumeKey)
        {
            if (isDown)
            {
                _dispatcher.InvokeAsync(() => VolumeKeyPressed?.Invoke(this, volumeKey));
            }

            // Swallow the release too, so no app sees half of a key press.
            return SuppressVolumeKeys;
        }

        if (SystemKeyMap.LockFromVirtualKey(virtualKey) is LockKey lockKey)
        {
            if (isUp)
            {
                _heldLockKeys.Remove(virtualKey);
            }
            else if (_heldLockKeys.Add(virtualKey))
            {
                _dispatcher.InvokeAsync(() => LockKeyPressed?.Invoke(this, lockKey));
            }

            return false;
        }

        if (isDown && MediaKeyMap.FromVirtualKey(virtualKey) is MediaKey mediaKey)
        {
            _dispatcher.InvokeAsync(() => MediaKeyPressed?.Invoke(this, mediaKey));
        }

        return false;
    }
}
