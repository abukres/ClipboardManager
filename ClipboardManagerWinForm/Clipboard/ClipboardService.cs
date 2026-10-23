using System.Runtime.InteropServices;
using System.Text;
using ClipboardManagerWinForm.Interop;
using ClipboardManagerWinForm.Services;

namespace ClipboardManagerWinForm.Clipboard;

public static class ClipboardService
{
    private const int RetryCount = 10;
    private const int RetryDelay = 25;

    public static bool TryGetText(out string text)
    {
        text = string.Empty;

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return false;
                }

                text = System.Windows.Forms.Clipboard.GetText(TextDataFormat.UnicodeText);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (ThreadStateException)
            {
                return false;
            }
        }

        return false;
    }

    public static bool SetText(string text)
    {
        // Only plain Unicode text is published: no "Rich Text Format" or "HTML Format" is offered, so a
        // destination cannot restore the formatting of the copied text and always pastes pure text.
        var plainText = TextFilters.ToPlainText(text);

        return SetUnicodeText(plainText) || SetTextWithDataObject(plainText);
    }

    private static bool SetUnicodeText(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + "\0");

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(RetryDelay);
                continue;
            }

            try
            {
                // Emptying the clipboard first removes every format of the previous content.
                if (NativeMethods.EmptyClipboard() &&
                    SetClipboardBlock(NativeMethods.CF_UNICODETEXT, bytes))
                {
                    return true;
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }

            Thread.Sleep(RetryDelay);
        }

        return false;
    }

    private static bool SetClipboardBlock(uint format, byte[] bytes)
    {
        var memory = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)(uint)bytes.Length);

        if (memory == IntPtr.Zero)
        {
            return false;
        }

        var target = NativeMethods.GlobalLock(memory);

        if (target == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            return false;
        }

        try
        {
            Marshal.Copy(bytes, 0, target, bytes.Length);
        }
        finally
        {
            NativeMethods.GlobalUnlock(memory);
        }

        // The clipboard owns the block once the call succeeds, so it must not be freed then.
        if (NativeMethods.SetClipboardData(format, memory) == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            return false;
        }

        return true;
    }

    private static bool SetTextWithDataObject(string text)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetDataObject(text, true, 5, RetryDelay);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        return false;
    }

    public static bool Clear()
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                System.Windows.Forms.Clipboard.Clear();
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        return false;
    }

    public static void SendPasteKeys()
    {
        var inputs = new[]
        {
            CreateKeyInput(NativeMethods.VK_CONTROL, keyUp: false),
            CreateKeyInput(NativeMethods.VK_V, keyUp: false),
            CreateKeyInput(NativeMethods.VK_V, keyUp: true),
            CreateKeyInput(NativeMethods.VK_CONTROL, keyUp: true)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT CreateKeyInput(int virtualKey, bool keyUp) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KEYBDINPUT
            {
                WVk = (ushort)virtualKey,
                Flags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0
            }
        }
    };
}
