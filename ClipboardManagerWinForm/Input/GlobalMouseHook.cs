using System.Runtime.InteropServices;
using ClipboardManagerWinForm.Interop;

namespace ClipboardManagerWinForm.Input;

public sealed class GlobalMouseHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private IntPtr _hook;

    public GlobalMouseHook()
    {
        _callback = HookCallback;
    }

    /// <summary>
    /// Raised for every mouse button press. The argument is the top level window under the cursor.
    /// </summary>
    public event EventHandler<IntPtr>? ButtonPressed;

    public void Install()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookExMouse(
            NativeMethods.WH_MOUSE_LL,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        Uninstall();
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && IsButtonDown(wParam.ToInt32()))
            {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var window = NativeMethods.GetAncestor(
                    NativeMethods.WindowFromPoint(info.Point),
                    NativeMethods.GA_ROOT);

                ButtonPressed?.Invoke(this, window);
            }
        }
        catch (Exception)
        {
            // A failing hook must never tear down the application.
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool IsButtonDown(int message)
    {
        return message is NativeMethods.WM_LBUTTONDOWN or
            NativeMethods.WM_RBUTTONDOWN or
            NativeMethods.WM_MBUTTONDOWN or
            NativeMethods.WM_XBUTTONDOWN;
    }
}
