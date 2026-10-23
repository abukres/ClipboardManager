using ClipboardManagerWinForm.Interop;

namespace ClipboardManagerWinForm.Input;

public sealed class HotKeyWindow : NativeWindow, IDisposable
{
    private const int HotKeyId = 0x4A01;

    private bool _registered;

    public HotKeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "ClipboardManager.HotKeyWindow" });
    }

    public event EventHandler? Pressed;

    public bool Register(int modifiers, Keys key)
    {
        Unregister();

        _registered = NativeMethods.RegisterHotKey(
            Handle,
            HotKeyId,
            modifiers | NativeMethods.MOD_NOREPEAT,
            (int)key);

        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(Handle, HotKeyId);
        _registered = false;
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
        {
            return;
        }

        Unregister();
        DestroyHandle();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref message);
    }
}
