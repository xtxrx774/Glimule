using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glimule;

internal sealed class AppSettings
{
    public bool CapsuleInTextField { get; set; } = true;

    public bool RunAtStartup { get; set; } = true;
    public bool UseDefaultScale { get; set; } = true;
    public int BubbleScale { get; set; } = 100;

    [JsonIgnore]
    public double ScaleFactor
    {
        get
        {
            if (UseDefaultScale)
            {
                return 1;
            }

            return Math.Clamp(BubbleScale, 50, 200) / 100.0;
        }
    }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true
    };

    private static readonly string Path = System.IO.Path.Combine(StartupService.InstallDir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static event EventHandler? Changed;

    public static void Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                var legacy = System.IO.Path.Combine(StartupService.LegacyInstallDir, "settings.json");

                if (File.Exists(legacy))
                {
                    Directory.CreateDirectory(StartupService.InstallDir);
                    File.Copy(legacy, Path, overwrite: false);
                }
            }

            if (File.Exists(Path))
            {
                var contents = File.ReadAllText(Path);
                Current = JsonSerializer.Deserialize<AppSettings>(contents) ?? new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings();
        }

        StartupService.SetEnabled(Current.RunAtStartup);

        var scale = Current.BubbleScale == 0 ? 100 : Current.BubbleScale;
        Current.BubbleScale = Math.Clamp(scale, 50, 200);
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(StartupService.InstallDir);

            var contents = JsonSerializer.Serialize(Current, Json);
            File.WriteAllText(Path, contents);
        }
        catch
        {
            // Settings are optional; the next change will try again.
        }

        StartupService.SetEnabled(Current.RunAtStartup);
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
