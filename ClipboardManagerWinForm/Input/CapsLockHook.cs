using System.Runtime.InteropServices;
using ClipboardManagerWinForm.Interop;

namespace ClipboardManagerWinForm.Input;

public sealed class CapsLockHook : IDisposable
{
    private readonly Action<Action> _dispatcher;
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private IntPtr _hook;
    private bool _enabled;

    public CapsLockHook(Action<Action> dispatcher)
    {
        _dispatcher = dispatcher;
        _callback = HookCallback;
    }

    public event EventHandler? CapsLockPressed;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;

            if (value)
            {
                Install();
            }
            else
            {
                Uninstall();
            }
        }
    }

    public void Dispose()
    {
        Uninstall();
    }

    private void Install()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    private void Uninstall()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _enabled)
        {
            var message = wParam.ToInt32();
            var virtualKey = Marshal.ReadInt32(lParam);

            if (virtualKey == NativeMethods.VK_CAPITAL)
            {
                if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                {
                    _dispatcher(() => CapsLockPressed?.Invoke(this, EventArgs.Empty));
                }

                return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }
}
