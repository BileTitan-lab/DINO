using System.Runtime.InteropServices;

namespace DinoDash.Desktop;

// Decode control keys before WinForms translates them into text. IME disabling
// is scoped to our controls; no global hooks or keyboard-layout changes.
internal static class GameKeyboard
{
    public static bool TryRead(in Message message, out int key, out bool down)
    {
        down = message.Msg is 0x0100 or 0x0104; // WM_KEYDOWN / WM_SYSKEYDOWN
        key = 0;
        if (!down && message.Msg is not (0x0101 or 0x0105)) return false;
        key = (int)((long)message.WParam & 0xffff);
        if (key == (int)Keys.ProcessKey)
        {
            // A few IMEs can still have a queued VK_PROCESSKEY when focus or
            // input language changes. ImmGetVirtualKey is valid before TranslateMessage.
            uint original = down && message.HWnd != IntPtr.Zero
                ? ImmGetVirtualKey(message.HWnd) : 0;
            if (original is 0 or (uint)Keys.ProcessKey)
            {
                uint scan = (uint)((long)message.LParam >> 16) & 0xff;
                if (scan == 0) return false;
                if (((long)message.LParam & (1L << 24)) != 0) scan |= 0xe000;
                original = MapVirtualKeyEx(scan, 3, GetKeyboardLayout(0));
            }
            key = (int)original;
        }
        // Bindings use the generic modifier virtual keys on both sides.
        key = (Keys)key switch
        {
            Keys.LShiftKey or Keys.RShiftKey => (int)Keys.ShiftKey,
            Keys.LControlKey or Keys.RControlKey => (int)Keys.ControlKey,
            Keys.LMenu or Keys.RMenu => (int)Keys.Menu,
            _ => key
        };
        return key > 0 && key != (int)Keys.ProcessKey;
    }

    [DllImport("imm32.dll", ExactSpelling = true)]
    private static extern uint ImmGetVirtualKey(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW", ExactSpelling = true)]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetKeyboardLayout(uint threadId);
}
