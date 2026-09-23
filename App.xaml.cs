using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace CursorUI;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\CursorUI.SingleInstance";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private IndicatorWindow? _window;
    private OverlayService? _overlay;
    private TrayService? _tray;
    private SettingsWindow? _settings;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        _ownsMutex = true;
        SettingsStore.Load();

        _window = new IndicatorWindow();
        _ = new WindowInteropHelper(_window).EnsureHandle();
        _window.Show();
        _window.HideOverlay();

        _overlay = new OverlayService(_window);
        _tray = new TrayService(OpenSettings, Shutdown);
        _overlay.Start();
        _tray.Show();
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settings == null)
            {
                _settings = new SettingsWindow();
                _settings.Closed += (_, _) => _settings = null;
            }

            _settings.Show();
            _settings.Activate();
            _settings.WindowState = WindowState.Normal;
        });
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        LanguageOverlay.Restore();
        _overlay?.Dispose();
        _tray?.Dispose();
        _window?.Close();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
