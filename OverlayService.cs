using System.Windows.Media;
using System.Windows.Threading;

namespace Glimule;

internal sealed class OverlayService : IDisposable
{
    private readonly IndicatorWindow _window;
    private readonly DispatcherTimer _timer;
    private ushort _lastLang;
    private string? _languageFlash;
    private string? _previousLabel;
    private DateTime _languageUntil;
    private bool _capsSuppressed;
    private bool _lastCaps;
    private CaretInfo _lastCaret;
    private bool _inTextField;
    private bool _pendingTextField;
    private DateTime _pendingSince;
    private uint _focusThread;
    private IntPtr _fieldRoot;
    private long _lastTypedTicks;
    private readonly KeyboardHook _keys = new();

    public OverlayService(IndicatorWindow window)
    {
        _window = window;
        _window.ApplyStyles(ReadAccent());
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            try { Tick(); }
            catch { /* keep the overlay alive */ }
        };
        _keys.WinSpace += OnWinSpace;
        _keys.Typing += OnTyping;
    }

    public void Start()
    {
        LanguageOverlay.Start();
        _timer.Start();
    }

    private void OnWinSpace()
    {
        LanguageOverlay.KickWinSpaceHide();
    }

    private void OnTyping()
    {
        Interlocked.Exchange(ref _lastTypedTicks, DateTime.UtcNow.Ticks);
    }

    private bool TypingIdle =>
        (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastTypedTicks), DateTimeKind.Utc)).TotalMilliseconds >= 420;

    private void Tick()
    {
        var caps = (Native.GetKeyState(Native.VkCapital) & 1) != 0;
        if (caps != _lastCaps)
        {
            _lastCaps = caps;
            _capsSuppressed = false;
        }

        if ((Native.GetKeyState(Native.VkEscape) & 0x8000) != 0 && caps)
        {
            _capsSuppressed = true;
        }

        var live = CaretLocator.Locate(allowSlow: true);
        if (live.ThreadId != 0) _focusThread = live.ThreadId;
        UpdateTextField(live);
        if (live.Ok && live.HwndRoot != IntPtr.Zero) _lastCaret = live;

        var lang = CurrentLanguageId();
        if (lang != 0 && lang != _lastLang)
        {
            if (_lastLang != 0)
            {
                _previousLabel = LayoutLabel.FromLangId(_lastLang);
                FlashLanguage(LayoutLabel.FromLangId(lang));
            }

            _lastLang = lang;
        }

        var smart = SettingsStore.Current.CapsuleInTextField;
        LanguageOverlay.SetAltShiftHidden(smart && _inTextField);
        if (LanguageOverlay.IsHidingPopups || (smart && _inTextField && DateTime.UtcNow < _languageUntil))
        {
            LanguageOverlay.HidePopups();
        }

        if (!smart)
        {
            _window.HideNow();
            return;
        }

        if (!_inTextField || live.HwndRoot == IntPtr.Zero)
        {
            _languageUntil = DateTime.MinValue;
            _lastCaret = default;
            _window.HideNow();
            return;
        }

        var caret = live.Ok ? live : (_lastCaret.HwndRoot == live.HwndRoot ? _lastCaret : default);
        if (!caret.Ok && live.HasTextFocus)
        {
            CaretLocator.TryFallbackInField(live, out caret);
        }

        var showCaps = caps && !_capsSuppressed && TypingIdle;
        var langLabel = DateTime.UtcNow < _languageUntil ? _languageFlash : null;

        if (!caret.Ok)
        {
            _window.HideNow();
            return;
        }

        _window.Follow(caret);
        if (!string.IsNullOrEmpty(langLabel))
        {
            var layouts = InstalledLayouts();
            var selected = Math.Max(0, layouts.FindIndex(x => x == langLabel));
            var from = string.IsNullOrEmpty(_previousLabel) ? selected : Math.Max(0, layouts.FindIndex(x => x == _previousLabel));
            _window.ShowLanguages(layouts, selected, from, caret);
        }
        else if (showCaps)
        {
            _window.ShowCaps(caret);
        }
        else
        {
            _window.HideNow();
        }
    }

    private void UpdateTextField(CaretInfo live)
    {
        var detected = live.HasTextFocus
                       && live.HwndRoot != IntPtr.Zero
                       && Native.IsWindow(live.HwndRoot)
                       && !Native.IsIconic(live.HwndRoot)
                       && Native.IsWindowVisible(live.HwndRoot);

        if (live.HwndRoot != _fieldRoot)
        {
            _fieldRoot = live.HwndRoot;
            _inTextField = detected;
            _pendingTextField = detected;
            _pendingSince = DateTime.UtcNow;
            return;
        }

        if (detected == _inTextField)
        {
            _pendingTextField = detected;
            return;
        }

        if (detected != _pendingTextField)
        {
            _pendingTextField = detected;
            _pendingSince = DateTime.UtcNow;
        }

        if (detected || (DateTime.UtcNow - _pendingSince).TotalMilliseconds >= 80)
        {
            _inTextField = detected;
        }
    }

    private List<string> InstalledLayouts()
    {
        var count = Native.GetKeyboardLayoutList(0, null);
        if (count <= 0) return new List<string> { CurrentLanguageLabel() };

        var ids = new IntPtr[count];
        Native.GetKeyboardLayoutList(count, ids);
        var labels = new List<string>();
        foreach (var id in ids)
        {
            var label = LayoutLabel.FromLangId(unchecked((ushort)id.ToInt64()));
            if (!labels.Contains(label)) labels.Add(label);
        }

        if (labels.Count == 0) labels.Add(CurrentLanguageLabel());
        return labels;
    }

    private void FlashLanguage(string label)
    {
        _languageFlash = label;
        _languageUntil = DateTime.UtcNow.AddMilliseconds(900);
    }

    private ushort CurrentLanguageId()
    {
        var tid = _focusThread;
        if (tid == 0)
        {
            var hwnd = CaretLocator.ForegroundWindow();
            if (hwnd == IntPtr.Zero) return 0;
            tid = Native.GetWindowThreadProcessId(hwnd, out _);
        }

        return unchecked((ushort)Native.GetKeyboardLayout(tid).ToInt64());
    }

    private string CurrentLanguageLabel() => LayoutLabel.FromLangId(CurrentLanguageId());

    private static System.Windows.Media.Color ReadAccent()
    {
        try
        {
            if (Native.DwmGetColorizationColor(out var color, out _) == 0)
            {
                var r = (byte)((color >> 16) & 0xFF);
                var g = (byte)((color >> 8) & 0xFF);
                var b = (byte)(color & 0xFF);
                if (r + g + b > 40)
                {
                    return System.Windows.Media.Color.FromRgb(r, g, b);
                }
            }
        }
        catch
        {
            // default accent
        }

        return System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xFF);
    }

    public void Dispose()
    {
        _timer.Stop();
        _keys.Dispose();
        LanguageOverlay.Restore();
        _window.HideNow();
    }
}
