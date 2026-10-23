using System.Runtime.InteropServices;

namespace ClipboardManagerWinForm.Interop;

internal sealed class MessageDispatcher : NativeWindow, IDisposable
{
    private const int WmExecute = 0x0400 + 0x51;

    private readonly Queue<Action> _queue = new();
    private readonly Lock _lock = new();

    public MessageDispatcher()
    {
        CreateHandle(new CreateParams { Caption = "ClipboardManager.Dispatcher" });
    }

    public void Post(Action action)
    {
        lock (_lock)
        {
            _queue.Enqueue(action);
        }

        PostMessage(Handle, WmExecute, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmExecute)
        {
            Action? action = null;

            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    action = _queue.Dequeue();
                }
            }

            action?.Invoke();
            return;
        }

        base.WndProc(ref message);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
