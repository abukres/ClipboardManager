namespace ClipboardManagerWinForm.Interop;

/// <summary>
///  Windows only lets a process take the foreground when it received the last input event, which is not
///  the case for a clipboard manager that opens a window from a global hotkey or a hook.
/// </summary>
internal static class WindowActivation
{
    public static void BringToForeground(IntPtr handle)
    {
        if (handle == IntPtr.Zero || NativeMethods.GetForegroundWindow() == handle)
        {
            return;
        }

        if (NativeMethods.SetForegroundWindow(handle))
        {
            return;
        }

        // Sharing the input queue of the window that owns the foreground lifts the restriction for the
        // duration of the call, which is the documented workaround for background processes.
        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        var attached = foregroundThread != 0 &&
                       foregroundThread != currentThread &&
                       NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}
