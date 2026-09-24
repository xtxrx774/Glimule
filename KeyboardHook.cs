using System.Runtime.InteropServices;

namespace Glimule;

internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.LowLevelKeyboardProc _proc;
    private readonly IntPtr _hook;

    public event Action? WinSpace;
    public event Action? Typing;

    public KeyboardHook()
    {
        _proc = OnKey;
        var module = System.IO.Path.GetFileName(Environment.ProcessPath ?? "Glimule.exe");
        _hook = Native.SetWindowsHookEx(Native.WhKeyboardLl, _proc, Native.GetModuleHandle(module), 0);
    }

    private IntPtr OnKey(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)Native.WmKeyDown || wParam == (IntPtr)Native.WmSysKeyDown))
        {
            var vk = Marshal.ReadInt32(lParam);
            var win = (Native.GetAsyncKeyState(Native.VkLwin) & 0x8000) != 0
                      || (Native.GetAsyncKeyState(Native.VkRwin) & 0x8000) != 0;
            try
            {
                if (vk == Native.VkSpace && win) WinSpace?.Invoke();
                else if (IsTypingKey(vk, win)) Typing?.Invoke();
            }
            catch
            {
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsTypingKey(int vk, bool winHeld)
    {
        if (winHeld) return false;
        if (vk is Native.VkCapital or Native.VkEscape or Native.VkLwin or Native.VkRwin) return false;
        if (vk is 0x10 or 0x11 or 0x12) return false; // Shift, Ctrl, Alt
        if (vk is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5) return false; // left/right modifiers
        if (vk is >= 0x70 and <= 0x87) return false; // function keys
        return true;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
    }
}
