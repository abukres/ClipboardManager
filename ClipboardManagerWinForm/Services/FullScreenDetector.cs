using ClipboardManagerWinForm.Interop;

namespace ClipboardManagerWinForm.Services;

public static class FullScreenDetector
{
    private const int QunsRunningD3DFullScreen = 3;
    private const int QunsPresentationMode = 4;

    public static bool IsFullScreenApplicationActive()
    {
        try
        {
            if (NativeMethods.SHQueryUserNotificationState(out var state) != 0)
            {
                return false;
            }

            return state is QunsRunningD3DFullScreen or QunsPresentationMode;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
