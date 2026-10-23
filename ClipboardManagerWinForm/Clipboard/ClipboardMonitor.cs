using ClipboardManagerWinForm.Interop;

namespace ClipboardManagerWinForm.Clipboard;

public sealed class ClipboardMonitor : NativeWindow, IDisposable
{
    private bool _listening;

    public ClipboardMonitor()
    {
        CreateHandle(new CreateParams { Caption = "ClipboardManager.ClipboardMonitor" });
        _listening = NativeMethods.AddClipboardFormatListener(Handle);
    }

    public event EventHandler? ClipboardUpdated;

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
        {
            return;
        }

        if (_listening)
        {
            NativeMethods.RemoveClipboardFormatListener(Handle);
            _listening = false;
        }

        DestroyHandle();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            ClipboardUpdated?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref message);
    }
}
