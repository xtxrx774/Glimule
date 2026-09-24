using Microsoft.Win32;
using System.IO;

namespace Glimule;

internal static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Glimule";
    private const string LegacyValueName = "CursorUI";

    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glimule");

    public static string LegacyInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorUI");

    public static void Enable()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return;

        var stable = Path.Combine(InstallDir, "Glimule.exe");
        if (File.Exists(stable)) exe = stable;

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? Registry.CurrentUser.CreateSubKey(KeyPath);
        key.DeleteValue(LegacyValueName, false);
        key.SetValue(ValueName, $"\"{exe}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        key?.DeleteValue(ValueName, false);
        key?.DeleteValue(LegacyValueName, false);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
