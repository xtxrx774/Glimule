using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Glimule;

public partial class IndicatorWindow : Window
{
    private bool _shown;
    private bool _caps;
    private int _selected;
    private CaretInfo _caret;
    private readonly List<string> _labels = new();
    private readonly List<double> _itemWidths = new();
    private SolidColorBrush _accent = new(Color.FromRgb(0x00, 0x7A, 0xFF));
    private SolidColorBrush _onAccent = new(Colors.White);
    private readonly SolidColorBrush _shell = CreateShell();

    private static SolidColorBrush CreateShell()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0xE6, 0x2C, 0x2C, 0x2E));
        brush.Freeze();
        return brush;
    }

    public IndicatorWindow()
    {
        InitializeComponent();
        Opacity = 1;
        Visibility = Visibility.Hidden;
        SettingsStore.Changed += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            ApplyUserScale();
            if (_shown) Position();
        });
        ApplyUserScale();
    }

    public void ApplyStyles(System.Windows.Media.Color accent)
    {
        _accent = new SolidColorBrush(accent);
        _accent.Freeze();
        var light = accent.R * 0.3 + accent.G * 0.59 + accent.B * 0.11 > 155;
        _onAccent = new SolidColorBrush(light ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White);
        _onAccent.Freeze();
        Selection.Background = _accent;
        CapsArrow.Fill = Brushes.Transparent;
        CapsArrow.Stroke = _onAccent;
        CapsBar.Fill = Brushes.Transparent;
        CapsBar.Stroke = _onAccent;
    }

    private IntPtr _owner;

    internal void Follow(CaretInfo caret)
    {
        if (!caret.Ok) return;
        _caret = caret;
        AttachTo(caret.HwndRoot);
        if (_shown) Position();
    }

    internal void ShowCaps(CaretInfo caret)
    {
        Follow(caret);
        if (_shown && _caps) return;
        _caps = true;
        Shell.Background = _accent;
        Langs.Visibility = Visibility.Collapsed;
        Selection.Visibility = Visibility.Collapsed;
        CapsHost.Opacity = 1;
        Selection.Width = 22;
        Canvas.SetLeft(Selection, 0);
        if (!_shown) PopIn(26);
        else AnimateWidth(26);
    }

    internal void ShowLanguages(IReadOnlyList<string> labels, int selected, int fromSelected, CaretInfo caret)
    {
        Follow(caret);
        if (labels.Count == 0) return;
        selected = Math.Clamp(selected, 0, labels.Count - 1);
        fromSelected = Math.Clamp(fromSelected, 0, labels.Count - 1);

        var same = _shown && !_caps && _labels.SequenceEqual(labels);
        BuildItems(labels);
        CapsHost.Opacity = 0;
        Langs.Visibility = Visibility.Visible;
        Selection.Visibility = Visibility.Visible;
        Shell.Background = _shell;

        var expanded = TotalWidth();
        var targetLeft = OffsetOf(selected);
        var targetWidth = ItemWidth(labels[selected]);

        if (!_shown)
        {
            _caps = false;
            _selected = selected;
            Selection.Width = ItemWidth(labels[fromSelected]);
            Canvas.SetLeft(Selection, OffsetOf(fromSelected));
            Recolor();
            PopIn(ItemWidth(labels[fromSelected]) + 4);
            AnimateWidth(expanded);
            if (fromSelected != selected)
            {
                AnimateSelection(targetLeft, targetWidth);
            }
            else
            {
                Canvas.SetLeft(Selection, targetLeft);
                Selection.Width = targetWidth;
            }

            return;
        }

        if (same && _selected == selected) return;

        _caps = false;
        _selected = selected;
        Recolor();
        AnimateWidth(expanded);
        AnimateSelection(targetLeft, targetWidth);
    }

    internal void HideNow()
    {
        _shown = false;
        _caps = false;
        Shell.BeginAnimation(OpacityProperty, null);
        Shell.BeginAnimation(WidthProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Shell.Opacity = 0;
        Visibility = Visibility.Hidden;
        AttachTo(IntPtr.Zero);
    }

    private void AttachTo(IntPtr owner)
    {
        if (_owner == owner) return;
        try
        {
            var helper = new WindowInteropHelper(this);
            helper.Owner = owner;
            _owner = owner;
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;
            Native.SetWindowPos(
                hwnd,
                Native.HwndNoTopmost,
                0, 0, 0, 0,
                Native.SwpNoMove | Native.SwpNoSize | Native.SwpNoActivate);
            if (owner != IntPtr.Zero)
            {
                Native.SetWindowPos(
                    hwnd,
                    Native.HwndTop,
                    0, 0, 0, 0,
                    Native.SwpNoMove | Native.SwpNoSize | Native.SwpNoActivate);
            }
        }
        catch
        {
            _owner = IntPtr.Zero;
        }
    }

    private void BuildItems(IReadOnlyList<string> labels)
    {
        if (_labels.SequenceEqual(labels) && Langs.Children.Count == labels.Count) return;
        _labels.Clear();
        _labels.AddRange(labels);
        _itemWidths.Clear();
        Langs.Children.Clear();
        foreach (var label in labels)
        {
            var width = ItemWidth(label);
            _itemWidths.Add(width);
            var text = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xCC)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var cell = new Border
            {
                Width = width,
                Height = 22,
                Child = text
            };
            Langs.Children.Add(cell);
        }
    }

    private void Recolor()
    {
        for (var i = 0; i < Langs.Children.Count; i++)
        {
            if (Langs.Children[i] is not Border cell || cell.Child is not TextBlock text) continue;
            text.Foreground = i == _selected ? _onAccent : new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xCC));
        }
    }

    private void PopIn(double startWidth)
    {
        _shown = true;
        Shell.BeginAnimation(OpacityProperty, null);
        Shell.BeginAnimation(WidthProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        Shell.Width = startWidth;
        Visibility = Visibility.Visible;
        Position();

        ShellScale.ScaleX = 0.78;
        ShellScale.ScaleY = 0.78;
        Shell.Opacity = 0;

        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var scale = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(280) };
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.78, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });

        Shell.BeginAnimation(OpacityProperty, opacity);
        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());
    }

    private void PopOut()
    {
        var opacity = new DoubleAnimation(Shell.Opacity, 0, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        opacity.Completed += (_, _) =>
        {
            if (_shown) return;
            Visibility = Visibility.Hidden;
        };
        var scale = new DoubleAnimation(1, 0.92, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Shell.BeginAnimation(OpacityProperty, opacity);
        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());
    }

    private void AnimateWidth(double width)
    {
        var current = double.IsNaN(Shell.Width) ? width : Shell.Width;
        var anim = new DoubleAnimation(current, width + 4, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) => Position();
        Shell.BeginAnimation(WidthProperty, anim);
        Position();
    }

    private void AnimateSelection(double left, double width)
    {
        var fromLeft = Canvas.GetLeft(Selection);
        if (double.IsNaN(fromLeft)) fromLeft = 0;
        Selection.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(fromLeft, left, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        var fromW = Selection.Width;
        Selection.BeginAnimation(WidthProperty, new DoubleAnimation(fromW, width, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private double OffsetOf(int index)
    {
        double x = 0;
        for (var i = 0; i < index && i < _itemWidths.Count; i++) x += _itemWidths[i];
        return x;
    }

    private double TotalWidth()
    {
        double w = 0;
        foreach (var item in _itemWidths) w += item;
        return Math.Max(22, w);
    }

    private static double ItemWidth(string label) => label.Length > 1 ? 34 : 22;

    private double Scale => Math.Clamp(SettingsStore.Current.ScaleFactor, 0.5, 2.0);

    private void ApplyUserScale()
    {
        var s = Scale;
        UserScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        UserScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        UserScale.ScaleX = s;
        UserScale.ScaleY = s;
    }

    private void Position()
    {
        if (!_caret.Ok) return;
        ApplyUserScale();
        DpiForPoint(_caret.X, _caret.Y, out var scaleX, out var scaleY);
        var caretDipX = _caret.X / scaleX;
        var caretDipY = _caret.Y / scaleY;
        var caretH = Math.Max(12, _caret.Height / scaleY);
        var layoutW = double.IsNaN(Shell.Width) ? 26 : Shell.Width;
        var s = Scale;
        var bubbleW = Math.Max(22, layoutW) * s;
        var bubbleH = 26 * s;
        var gap = 5 + 7 * s;

        const double canvasX = 10;
        const double canvasY = 8;
        var visualLeft = caretDipX;
        var visualTop = caretDipY + caretH + gap;
        if (_caret.HwndRoot != IntPtr.Zero && Native.GetWindowRect(_caret.HwndRoot, out var wr))
        {
            var winBottom = wr.Bottom / scaleY;
            var winRight = wr.Right / scaleX;
            var winLeft = wr.Left / scaleX;
            var winTop = wr.Top / scaleY;
            if (visualTop + bubbleH > winBottom - 4)
            {
                visualTop = caretDipY - gap - bubbleH;
            }

            if (visualTop < winTop + 4)
            {
                visualTop = Math.Max(winTop + 4, caretDipY + caretH + gap);
            }

            if (visualLeft + bubbleW > winRight - 6)
            {
                visualLeft = Math.Max(winLeft + 6, winRight - 6 - bubbleW);
            }

            if (visualLeft < winLeft + 4) visualLeft = winLeft + 4;
        }

        Left = visualLeft - canvasX;
        Top = visualTop - canvasY;
        Width = Math.Max(64, bubbleW + 36);
        Height = Math.Max(48, bubbleH + 32);
        Canvas.SetLeft(Shell, canvasX);
        Canvas.SetTop(Shell, canvasY);
    }

    private static void DpiForPoint(double x, double y, out double scaleX, out double scaleY)
    {
        var pt = new Native.Point { X = (int)Math.Round(x), Y = (int)Math.Round(y) };
        var monitor = Native.MonitorFromPoint(pt, Native.MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero
            && Native.GetDpiForMonitor(monitor, Native.MdtEffectiveDpi, out var dpiX, out var dpiY) == 0
            && dpiX > 0 && dpiY > 0)
        {
            scaleX = dpiX / 96.0;
            scaleY = dpiY / 96.0;
            return;
        }

        scaleX = 1;
        scaleY = 1;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var style = Native.GetWindowLong(hwnd, Native.GwlExStyle);
        Native.SetWindowLong(
            hwnd,
            Native.GwlExStyle,
            style | Native.WsExNoActivate | Native.WsExToolWindow | Native.WsExTransparent | Native.WsExLayered);
    }
}
