using Microsoft.Win32;

namespace Glimule;

internal static class LanguageOverlay
{
    private const string KeyPath = @"Keyboard Layout\Toggle";
    private const int HideDelay = 100_000_000;
    private static bool _delayOn;
    private static bool _winSpaceOff;
    private static object? _savedDelay;
    private static object? _savedMulti;
    private static DateTime _hideUntil;
    private static Native.EnumWindowsProc? _enum;

    public static bool IsHidingPopups => DateTime.UtcNow < _hideUntil;

    public static void Start()
    {
        HideWinSpaceOverlay(true);
        SetAltShiftHidden(false);
    }

    public static void SetAltShiftHidden(bool hide)
    {
        if (hide == _delayOn) return;
        ApplyDelay(hide);
    }

    public static void HideWinSpaceOverlay(bool hide)
    {
        if (hide == _winSpaceOff) return;
        ApplyWinSpace(hide);
    }

    public static void KickWinSpaceHide()
    {
        _hideUntil = DateTime.UtcNow.AddMilliseconds(1200);
        HidePopups();
    }

    public static void HidePopups()
    {
        _enum ??= OnWindow;
        Native.EnumWindows(_enum, IntPtr.Zero);
    }

    private static void ApplyDelay(bool hide)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true)
                             ?? Registry.CurrentUser.CreateSubKey(KeyPath);
            if (hide)
            {
                var delay = key.GetValue("HotkeyShowDelayInMS");
                if (!IsOurDelay(delay)) _savedDelay = delay;
                key.SetValue("HotkeyShowDelayInMS", HideDelay, RegistryValueKind.DWord);
                _delayOn = true;
            }
            else
            {
                RestoreValue(key, "HotkeyShowDelayInMS", _savedDelay);
                _savedDelay = null;
                _delayOn = false;
            }
        }
        catch
        {
            _delayOn = hide;
        }
    }

    private static void ApplyWinSpace(bool hide)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true)
                             ?? Registry.CurrentUser.CreateSubKey(KeyPath);
            if (hide)
            {
                var multi = key.GetValue("InputSwitcherShowOnMultiPress");
                if (multi is not int and not long || Convert.ToInt32(multi) != 0)
                {
                    _savedMulti = multi;
                }

                key.SetValue("InputSwitcherShowOnMultiPress", 0, RegistryValueKind.DWord);
                _winSpaceOff = true;
            }
            else
            {
                RestoreValue(key, "InputSwitcherShowOnMultiPress", _savedMulti);
                _savedMulti = null;
                _winSpaceOff = false;
            }
        }
        catch
        {
            _winSpaceOff = hide;
        }
    }

    private static bool IsOurDelay(object? value) =>
        value is int i && i >= 90_000_000
        || value is long l && l >= 90_000_000;

    private static void RestoreValue(RegistryKey key, string name, object? saved)
    {
        if (saved == null) key.DeleteValue(name, false);
        else key.SetValue(name, saved);
    }

    private static bool OnWindow(IntPtr hwnd, IntPtr lParam)
    {
        if (!Native.IsWindowVisible(hwnd) || Native.IsOurWindow(hwnd)) return true;

        var cls = Native.ClassName(hwnd);
        if (cls.Contains("Cicero", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("InputSwitch", StringComparison.OrdinalIgnoreCase)
            || cls.Equals("IME", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("MSCTFIME", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("TF_FloatingLangBar", StringComparison.OrdinalIgnoreCase))
        {
            Native.ShowWindow(hwnd, Native.SwHide);
            return true;
        }

        if (cls is "Windows.UI.Core.CoreWindow" or "Windows.UI.Input.InputSite.WindowClass")
        {
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            try
            {
                var name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
                if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Native.GetWindowRect(hwnd, out var rect)) return true;
                    var w = rect.Right - rect.Left;
                    var h = rect.Bottom - rect.Top;
                    if (w is > 80 and < 720 && h is > 40 and < 360)
                    {
                        Native.ShowWindow(hwnd, Native.SwHide);
                    }
                }
            }
            catch
            {
                // process may have exited
            }
        }

        return true;
    }

    public static void Restore()
    {
        _hideUntil = DateTime.MinValue;
        SetAltShiftHidden(false);
        HideWinSpaceOverlay(false);
    }
}
