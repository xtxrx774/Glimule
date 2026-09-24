using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace Glimule;

internal readonly record struct CaretInfo(
    double X,
    double Y,
    double Width,
    double Height,
    bool Ok,
    bool HasTextFocus,
    IntPtr HwndCaret,
    uint ThreadId,
    IntPtr HwndRoot,
    Native.Rect Field);

internal static class CaretLocator
{
    private static readonly Guid IidAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
    private static CaretInfo _cached;
    private static DateTime _cacheAt;

    public static IntPtr ForegroundWindow() => RealForegroundWindow();

    public static CaretInfo Locate(bool allowSlow = true)
    {
        var hwnd = RealForegroundWindow();
        if (hwnd == IntPtr.Zero) return default;

        var root = Native.GetAncestor(hwnd, Native.GaRoot);
        if (root == IntPtr.Zero) root = hwnd;
        if (!Native.IsWindow(root) || Native.IsIconic(root) || !Native.IsWindowVisible(root))
        {
            _cached = default;
            return default;
        }

        var info = new Native.GuiThreadInfo { cbSize = MarshalSize() };
        var tid = Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId) return default;
        if (!Native.GetGUIThreadInfo(0, ref info) && !Native.GetGUIThreadInfo(tid, ref info))
        {
            return default;
        }

        var focus = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : hwnd;
        if (Native.IsOurWindow(focus) || Native.IsOurWindow(hwnd)) return default;

        var threadId = Native.GetWindowThreadProcessId(focus, out _);
        var cls = Native.ClassName(focus);
        var attachedFocus = IntPtr.Zero;
        if (!IsTextInputTarget(focus) && !IsTextInputTarget(info.hwndCaret) && !IsDedicatedEditor(focus, info))
        {
            _cached = default;
            return default;
        }

        var editor = ResolveEditorHwnd(focus, info.hwndCaret);
        if (TryGuiCaret(info, threadId, out var caret)) return Remember(caret);
        if (TryScintillaCaret(editor, threadId, out caret)) return Remember(caret);
        if (TryEditPosCaret(editor, threadId, out caret)) return Remember(caret);
        if (TryAttachedCaret(focus, threadId, out caret, out attachedFocus)) return Remember(caret);
        if (TryAccCaret(info.hwndCaret, focus, attachedFocus, threadId, out caret)) return Remember(caret);
        if (TryImeCaret(focus, info.hwndCaret, threadId, out caret)) return Remember(caret);

        if (allowSlow && TryUiaCaret(focus, threadId, out caret)) return Remember(caret);

        if (_cached.Ok && _cached.HwndRoot == root && (DateTime.UtcNow - _cacheAt).TotalMilliseconds < 500)
        {
            return _cached;
        }

        var textFocus = info.hwndCaret != IntPtr.Zero
                        || (info.flags & Native.GuiCaretBlinking) != 0
                        || IsEditClass(cls)
                        || IsDedicatedEditorClass(cls)
                        || IsTextInputTarget(focus);

        if (textFocus)
        {
            var probe = WithField(new CaretInfo(0, 0, 0, 0, false, true, editor != IntPtr.Zero ? editor : focus, threadId, IntPtr.Zero, default), editor != IntPtr.Zero ? editor : focus);
            if (TryFallbackInField(probe, out caret)) return Remember(caret);
            return probe;
        }

        _cached = default;
        return default;
    }

    private static CaretInfo Remember(CaretInfo caret)
    {
        _cached = caret;
        _cacheAt = DateTime.UtcNow;
        return caret;
    }

    private static bool TryGuiCaret(Native.GuiThreadInfo info, uint threadId, out CaretInfo caret)
    {
        caret = default;
        if (info.hwndCaret == IntPtr.Zero) return false;
        var rect = info.rcCaret;
        if (IsEmpty(rect))
        {
            if (IsRootSized(info.hwndCaret)) return false;
            if (!Native.GetClientRect(info.hwndCaret, out var client)) return false;
            var ch = Math.Max(12, client.Bottom - client.Top);
            rect = new Native.Rect
            {
                Left = 4,
                Top = Math.Max(0, (ch - 16) / 2),
                Right = 6,
                Bottom = Math.Max(16, (ch - 16) / 2 + 16)
            };
        }
        _ = Native.MapWindowPoints(info.hwndCaret, IntPtr.Zero, ref rect, 2);
        if (!IsRealCaret(rect, info.hwndCaret)) return false;
        caret = ToInfo(rect, info.hwndCaret, threadId);
        return true;
    }

    private static bool TryAttachedCaret(IntPtr focus, uint threadId, out CaretInfo caret, out IntPtr attachedFocus)
    {
        caret = default;
        attachedFocus = IntPtr.Zero;
        var mine = Native.GetCurrentThreadId();
        var attached = threadId != mine && Native.AttachThreadInput(mine, threadId, true);
        try
        {
            attachedFocus = Native.GetFocus();
            var origin = attachedFocus != IntPtr.Zero ? attachedFocus : focus;
            if (origin == IntPtr.Zero) return false;
            if (!Native.GetCaretPos(out var pos)) return false;
            var originClass = Native.ClassName(origin);
            var likelyFakeOrigin =
                Math.Abs(pos.X) < 2 && Math.Abs(pos.Y) < 2
                && IsRootSized(origin)
                && !IsEditClass(originClass)
                && !IsDedicatedEditorClass(originClass);
            if (likelyFakeOrigin) return false;
            if (!Native.ClientToScreen(origin, ref pos)) return false;
            var rect = new Native.Rect { Left = pos.X, Top = pos.Y, Right = pos.X + 2, Bottom = pos.Y + 16 };
            if (!IsRealCaret(rect, origin)) return false;
            caret = ToInfo(rect, origin, threadId);
            return true;
        }
        finally
        {
            if (attached) Native.AttachThreadInput(mine, threadId, false);
        }
    }

    private static bool TryAccCaret(IntPtr hwndCaret, IntPtr hwndFocus, IntPtr attachedFocus, uint threadId, out CaretInfo caret)
    {
        caret = default;
        foreach (var candidate in new[] { hwndCaret, attachedFocus, hwndFocus })
        {
            if (candidate == IntPtr.Zero) continue;
            if (!TryAccOn(candidate, Native.ObjIdCaret, out var rect)) continue;
            if (!IsRealCaret(rect, candidate)) continue;
            caret = ToInfo(rect, candidate, threadId);
            return true;
        }

        return false;
    }

    private static bool TryAccOn(IntPtr hwnd, uint objectId, out Native.Rect rect)
    {
        rect = default;
        try
        {
            var iid = IidAccessible;
            if (Native.AccessibleObjectFromWindow(hwnd, objectId, ref iid, out var obj) != 0 || obj is null)
            {
                return false;
            }

            int x, y, w, h;
            if (obj is Native.IAccessible acc)
            {
                acc.accLocation(out x, out y, out w, out h, Native.ChildIdSelf);
            }
            else
            {
                dynamic late = obj;
                late.accLocation(out x, out y, out w, out h, Native.ChildIdSelf);
            }

            if (w < 0) return false;
            if (h < 4) h = 16;
            rect = new Native.Rect { Left = x, Top = y, Right = x + Math.Max(w, 1), Bottom = y + h };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryImeCaret(IntPtr focus, IntPtr hwndCaret, uint threadId, out CaretInfo caret)
    {
        caret = default;
        foreach (var hwnd in new[] { focus, hwndCaret })
        {
            if (hwnd == IntPtr.Zero) continue;
            var imc = Native.ImmGetContext(hwnd);
            if (imc == IntPtr.Zero) continue;
            try
            {
                var form = new Native.CompositionForm { dwStyle = Native.CfsPoint };
                if (!Native.ImmGetCompositionWindow(imc, ref form)) continue;
                var pos = form.ptCurrentPos;
                if (!Native.ClientToScreen(hwnd, ref pos)) continue;
                var rect = new Native.Rect { Left = pos.X, Top = pos.Y, Right = pos.X + 2, Bottom = pos.Y + 16 };
                if (!IsRealCaret(rect, hwnd)) continue;
                caret = ToInfo(rect, hwnd, threadId);
                return true;
            }
            finally
            {
                Native.ImmReleaseContext(hwnd, imc);
            }
        }

        return false;
    }

    private static bool TryUiaCaret(IntPtr focus, uint threadId, out CaretInfo caret)
    {
        caret = default;
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return false;
            if (focused.Current.ProcessId == Environment.ProcessId) return false;

            var text = FindTextElement(focused);
            if (Native.ClassName(focus).Contains("HwndWrapper", StringComparison.OrdinalIgnoreCase)
                && TryWpfCaret(focused, focus, threadId, out caret))
            {
                return true;
            }
            if (text != null && TryTextCaret(text, focus, threadId, out caret))
            {
                caret = WithUiaField(caret, text);
                if (caret.Ok) return true;
                if (TryUiaBounds(text, focus, threadId, out caret)) return true;
                return TryFallbackInField(caret, out caret);
            }

            if (IsTextControl(focused) && TryUiaBounds(focused, focus, threadId, out caret)) return true;
            if (text != null && text != focused && TryUiaBounds(text, focus, threadId, out caret)) return true;
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryTextCaret(AutomationElement element, IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = default;
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var raw) || raw is not TextPattern text)
            {
                return IsTextControl(element) && MakeFocusOnly(hwnd, threadId, out caret);
            }

            var selection = text.GetSelection();
            if (selection.Length > 0)
            {
                var end = selection[^1];
                try
                {
                    end.MoveEndpointByRange(TextPatternRangeEndpoint.Start, end, TextPatternRangeEndpoint.End);
                }
                catch
                {
                    // some providers reject collapse
                }

                if (TryRangeRect(end, out var selRect) && IsPlausibleScreen(selRect))
                {
                    caret = ToInfo(selRect, hwnd, threadId);
                    caret = WithUiaField(caret, element);
                    return true;
                }
            }

            try
            {
                var visible = text.GetVisibleRanges();
                if (visible.Length > 0 && TryRangeRect(visible[^1], out var visRect) && IsPlausibleScreen(visRect))
                {
                    caret = ToInfo(visRect, hwnd, threadId);
                    caret = WithUiaField(caret, element);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                var start = text.DocumentRange;
                start.MoveEndpointByRange(TextPatternRangeEndpoint.End, start, TextPatternRangeEndpoint.Start);
                if (TryRangeRect(start, out var startRect) && IsPlausibleScreen(startRect))
                {
                    caret = ToInfo(startRect, hwnd, threadId);
                    caret = WithUiaField(caret, element);
                    return true;
                }
            }
            catch
            {
                // empty documents often have no character range
            }

            if (TryUiaBounds(element, hwnd, threadId, out caret)) return true;
            return IsTextControl(element) && MakeFocusOnly(hwnd, threadId, out caret);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRangeRect(TextPatternRange range, out Native.Rect rect)
    {
        rect = default;
        try
        {
            var rects = range.GetBoundingRectangles();
            if (TryPickRect(rects, out rect)) return true;
            range.ExpandToEnclosingUnit(TextUnit.Character);
            rects = range.GetBoundingRectangles();
            if (TryPickRect(rects, out rect)) return true;
            range.ExpandToEnclosingUnit(TextUnit.Line);
            rects = range.GetBoundingRectangles();
            if (rects.Length > 0 && rects[0].Height is >= 4 and <= 120)
            {
                var r = rects[0];
                var x = (int)Math.Round(r.X + r.Width);
                rect = new Native.Rect
                {
                    Left = x,
                    Top = (int)Math.Round(r.Y),
                    Right = x + 2,
                    Bottom = (int)Math.Round(r.Y + r.Height)
                };
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryPickRect(System.Windows.Rect[] rects, out Native.Rect rect)
    {
        rect = default;
        if (rects.Length == 0) return false;
        var r = rects[^1];
        if (r.Height > 140) return false;
        var h = r.Height < 4 ? 16 : r.Height;
        rect = new Native.Rect
        {
            Left = (int)Math.Round(r.X),
            Top = (int)Math.Round(r.Y),
            Right = (int)Math.Round(r.X + Math.Max(2, Math.Min(r.Width < 1 ? 2 : r.Width, 8))),
            Bottom = (int)Math.Round(r.Y + h)
        };
        return true;
    }

    private static bool TryWpfCaret(AutomationElement focused, IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = default;
        try
        {
            var found = focused.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "WpfCaret"));
            if (found == null) return false;
            var bounds = found.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Height is < 4 or > 120) return false;
            var rect = new Native.Rect
            {
                Left = (int)Math.Round(bounds.X),
                Top = (int)Math.Round(bounds.Y),
                Right = (int)Math.Round(bounds.X + Math.Max(2, bounds.Width)),
                Bottom = (int)Math.Round(bounds.Y + bounds.Height)
            };
            if (!IsPlausibleScreen(rect)) return false;
            caret = ToInfo(rect, hwnd, threadId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryUiaBounds(AutomationElement focused, IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = default;
        try
        {
            var bounds = focused.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width < 12 || bounds.Height < 8) return false;
            if (bounds.Height > 220 && bounds.Width > 400) return false;
            var rect = new Native.Rect
            {
                Left = (int)Math.Round(bounds.X + 8),
                Top = (int)Math.Round(bounds.Y + Math.Max(2, (bounds.Height - 16) / 2)),
                Right = (int)Math.Round(bounds.X + 10),
                Bottom = (int)Math.Round(bounds.Y + Math.Min(bounds.Height - 2, Math.Max(12, bounds.Height - 4)))
            };
            if (!IsPlausibleScreen(rect)) return false;
            caret = ToInfo(rect, hwnd, threadId);
            caret = WithUiaField(caret, focused);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryFallbackInField(CaretInfo probe, out CaretInfo caret)
    {
        caret = default;
        if (!probe.HasTextFocus) return false;
        var f = probe.Field;
        var fh = f.Bottom - f.Top;
        var fw = f.Right - f.Left;
        if (fh < 8 || fw < 16) return false;
        if (probe.HwndRoot != IntPtr.Zero && Native.GetWindowRect(probe.HwndRoot, out var wr))
        {
            var rh = Math.Max(1, wr.Bottom - wr.Top);
            var rw = Math.Max(1, wr.Right - wr.Left);
            if (fh > rh * 0.5 && fw > rw * 0.7) return false;
        }
        caret = probe with
        {
            Ok = true,
            X = f.Left + 8,
            Y = f.Top + Math.Max(2, (fh - 16) / 2.0),
            Width = 2,
            Height = Math.Clamp(fh - 4, 12, 28)
        };
        return true;
    }

    private static bool MakeFocusOnly(IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = WithField(new CaretInfo(0, 0, 0, 0, false, true, hwnd, threadId, IntPtr.Zero, default), hwnd);
        return true;
    }

    private static CaretInfo ToInfo(Native.Rect rect, IntPtr hwnd, uint threadId)
    {
        var w = Math.Max(2, rect.Right - rect.Left);
        var h = Math.Max(12, rect.Bottom - rect.Top);
        return WithField(new CaretInfo(rect.Left, rect.Top, w, h, true, true, hwnd, threadId, IntPtr.Zero, default), hwnd);
    }

    private static CaretInfo WithUiaField(CaretInfo caret, AutomationElement element)
    {
        try
        {
            var bounds = element.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width < 8 || bounds.Height < 8) return caret;
            var field = new Native.Rect
            {
                Left = (int)Math.Round(bounds.X),
                Top = (int)Math.Round(bounds.Y),
                Right = (int)Math.Round(bounds.X + bounds.Width),
                Bottom = (int)Math.Round(bounds.Y + bounds.Height)
            };
            if (caret.HwndRoot != IntPtr.Zero && Native.GetWindowRect(caret.HwndRoot, out var root))
            {
                field = Intersect(field, root);
            }

            return caret with { Field = field };
        }
        catch
        {
            return caret;
        }
    }

    private static CaretInfo WithField(CaretInfo caret, IntPtr hwnd)
    {
        var root = hwnd == IntPtr.Zero ? RealForegroundWindow() : Native.GetAncestor(hwnd, Native.GaRoot);
        if (root == IntPtr.Zero) root = hwnd;
        var field = FieldRect(hwnd, root);
        return caret with { HwndRoot = root, Field = field };
    }

    private static Native.Rect FieldRect(IntPtr hwnd, IntPtr root)
    {
        if (root != IntPtr.Zero && Native.GetWindowRect(root, out var rootRect))
        {
            if (hwnd != IntPtr.Zero && hwnd != root && Native.GetWindowRect(hwnd, out var control))
            {
                var ch = control.Bottom - control.Top;
                var cw = control.Right - control.Left;
                var rh = Math.Max(1, rootRect.Bottom - rootRect.Top);
                var rw = Math.Max(1, rootRect.Right - rootRect.Left);
                if (ch > 8 && ch < rh * 0.85 && cw > 12 && cw < rw * 0.99)
                {
                    return Intersect(control, rootRect);
                }
            }

            return rootRect;
        }

        if (hwnd != IntPtr.Zero && Native.GetWindowRect(hwnd, out var rect)) return rect;
        return default;
    }

    private static Native.Rect Intersect(Native.Rect a, Native.Rect b) => new()
    {
        Left = Math.Max(a.Left, b.Left),
        Top = Math.Max(a.Top, b.Top),
        Right = Math.Min(a.Right, b.Right),
        Bottom = Math.Min(a.Bottom, b.Bottom)
    };

    private static bool IsRealCaret(Native.Rect rect, IntPtr hwnd)
    {
        if (!IsPlausibleScreen(rect)) return false;
        var w = rect.Right - rect.Left;
        var host = hwnd == IntPtr.Zero ? RealForegroundWindow() : hwnd;
        var root = Native.GetAncestor(host, Native.GaRoot);
        if (root == IntPtr.Zero) root = host;
        if (root != IntPtr.Zero && Native.GetWindowRect(root, out var wr) && Native.GetWindowRect(host, out var hr))
        {
            var hw = hr.Right - hr.Left;
            var hh = hr.Bottom - hr.Top;
            var rw = Math.Max(1, wr.Right - wr.Left);
            var rh = Math.Max(1, wr.Bottom - wr.Top);
            var rootSized = hw >= rw * 0.65 && hh >= rh * 0.45;
            if (rootSized && rect.Left <= wr.Left + 24 && rect.Top <= wr.Top + 24 && w <= 8)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlausibleScreen(Native.Rect rect)
    {
        var w = rect.Right - rect.Left;
        var h = rect.Bottom - rect.Top;
        if (h is < 1 or > 160) return false;
        if (w > 64) return false;
        if (rect.Left < -200 || rect.Top < -200 || rect.Left > 16000 || rect.Top > 16000) return false;
        return true;
    }

    private static bool IsEmpty(Native.Rect rect) =>
        rect.Left == 0 && rect.Top == 0 && rect.Right == 0 && rect.Bottom == 0;

    private static bool IsRootSized(IntPtr hwnd)
    {
        var root = Native.GetAncestor(hwnd, Native.GaRoot);
        if (root == IntPtr.Zero) root = hwnd;
        if (!Native.GetWindowRect(hwnd, out var r) || !Native.GetWindowRect(root, out var rr)) return false;
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;
        var rw = Math.Max(1, rr.Right - rr.Left);
        var rh = Math.Max(1, rr.Bottom - rr.Top);
        return w >= rw * 0.65 && h >= rh * 0.45;
    }

    private static bool IsDedicatedEditor(IntPtr focus, Native.GuiThreadInfo info)
    {
        if (IsDedicatedEditorClass(Native.ClassName(focus))) return true;
        if (info.hwndCaret != IntPtr.Zero && IsDedicatedEditorClass(Native.ClassName(info.hwndCaret)))
            return true;
        return false;
    }

    private static bool IsTextInputTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var cls = Native.ClassName(hwnd);
        if (IsNonTextClass(cls)) return false;
        if (IsEditClass(cls) || IsDedicatedEditorClass(cls)) return true;
        if (cls.Contains("ComboBox", StringComparison.OrdinalIgnoreCase))
        {
            return (Native.GetStyle(hwnd) & Native.CbsDropDownList) != Native.CbsDropDownList;
        }

        return IsUiaTextInput();
    }

    private static bool IsNonTextClass(string cls)
    {
        if (string.IsNullOrEmpty(cls)) return false;
        if (cls.Equals("SysTreeView32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("SysListView32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("SysHeader32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("SysTabControl32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("ToolbarWindow32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("ReBarWindow32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("Button", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("Static", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("ScrollBar", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("ListBox", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("ComboLBox", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("tooltips_class32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("#32768", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("msctls_statusbar32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("msctls_trackbar32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("msctls_progress32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("SysMonthCal32", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("TreeView", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("ListView", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsDedicatedEditorClass(string cls)
    {
        if (string.IsNullOrEmpty(cls)) return false;
        if (cls.Equals("PX_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("AkelEdit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("SynEdit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("EmEditor", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("VsText", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("DesktopChildSiteBridge", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("InputSite.WindowClass", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("QPlainTextEdit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("QTextEdit", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsEditClass(string cls)
    {
        if (string.IsNullOrEmpty(cls)) return false;
        if (cls.Equals("Edit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("RICHEDIT", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("Scintilla", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("TextBox", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("TMemo", StringComparison.OrdinalIgnoreCase) || cls.Equals("TEdit", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("Internet Explorer_Server", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.StartsWith("_WwG", StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Equals("EXCEL7", StringComparison.OrdinalIgnoreCase) || cls.Equals("EXCELE", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsUiaTextInput()
    {
        try
        {
            var el = AutomationElement.FocusedElement;
            if (el == null) return false;
            if (el.Current.ProcessId == Environment.ProcessId) return false;

            for (var i = 0; i < 8 && el != null; i++)
            {
                ControlType type;
                try { type = el.Current.ControlType; }
                catch { return false; }

                if (type == ControlType.Edit || type == ControlType.Document) return true;
                if (type == ControlType.ComboBox) return UiaComboEditable(el);
                if (IsNonTextControl(type)) return false;
                el = TreeWalker.ControlViewWalker.GetParent(el);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool HasTextPattern(AutomationElement element)
    {
        try
        {
            var value = element.GetCurrentPropertyValue(AutomationElement.IsTextPatternAvailableProperty);
            if (value is true) return true;
        }
        catch
        {
            // some providers reject the property
        }

        try
        {
            return element.TryGetCurrentPattern(TextPattern.Pattern, out _);
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement? FindTextElement(AutomationElement focused)
    {
        try
        {
            for (var el = focused; el != null;)
            {
                if (HasTextPattern(el) || IsTextControl(el)) return el;
                ControlType type;
                try { type = el.Current.ControlType; }
                catch { break; }
                if (IsNonTextControl(type)) break;
                el = TreeWalker.ControlViewWalker.GetParent(el);
            }
        }
        catch
        {
            return focused;
        }

        return HasTextPattern(focused) || IsTextControl(focused) ? focused : null;
    }

    private static IntPtr ResolveEditorHwnd(IntPtr focus, IntPtr hwndCaret)
    {
        foreach (var candidate in new[] { hwndCaret, focus })
        {
            if (candidate == IntPtr.Zero) continue;
            var cls = Native.ClassName(candidate);
            if (IsEditClass(cls) || IsDedicatedEditorClass(cls)) return candidate;
        }

        if (focus != IntPtr.Zero)
        {
            var scintilla = FindChildByClass(focus, "Scintilla");
            if (scintilla != IntPtr.Zero) return scintilla;
        }

        return focus;
    }

    private static IntPtr FindChildByClass(IntPtr parent, string classPrefix)
    {
        var found = IntPtr.Zero;
        Native.EnumChildWindows(parent, (hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            if (Native.ClassName(hwnd).Contains(classPrefix, StringComparison.OrdinalIgnoreCase))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    private const int SciGetCurrentPos = 2008;
    private const int SciPointXFromPosition = 2164;
    private const int SciPointYFromPosition = 2165;
    private const int SciTextHeight = 2279;
    private const int SciLineFromPosition = 2166;
    private const int EmGetSel = 0x00B0;
    private const int EmPosFromChar = 0x00D6;

    private static bool TryScintillaCaret(IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = default;
        if (hwnd == IntPtr.Zero) return false;
        if (!Native.ClassName(hwnd).Contains("Scintilla", StringComparison.OrdinalIgnoreCase)) return false;

        var pos = Native.SendMessage(hwnd, SciGetCurrentPos, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (pos < 0) return false;
        var x = Native.SendMessage(hwnd, SciPointXFromPosition, IntPtr.Zero, (IntPtr)pos).ToInt32();
        var y = Native.SendMessage(hwnd, SciPointYFromPosition, IntPtr.Zero, (IntPtr)pos).ToInt32();
        var line = Native.SendMessage(hwnd, SciLineFromPosition, (IntPtr)pos, IntPtr.Zero).ToInt32();
        var height = Native.SendMessage(hwnd, SciTextHeight, (IntPtr)line, IntPtr.Zero).ToInt32();
        if (height is < 8 or > 96) height = 16;
        if (!IsPointInClient(hwnd, x, y)) return false;

        var pt = new Native.Point { X = x, Y = y };
        if (!Native.ClientToScreen(hwnd, ref pt)) return false;
        var rect = new Native.Rect { Left = pt.X, Top = pt.Y, Right = pt.X + 2, Bottom = pt.Y + height };
        if (!IsPlausibleScreen(rect)) return false;
        caret = ToInfo(rect, hwnd, threadId);
        return true;
    }

    private static bool TryEditPosCaret(IntPtr hwnd, uint threadId, out CaretInfo caret)
    {
        caret = default;
        if (hwnd == IntPtr.Zero) return false;
        var cls = Native.ClassName(hwnd);
        if (!cls.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            && !cls.StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase)
            && !cls.Equals("TEdit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var packedSel = Native.SendMessage(hwnd, EmGetSel, IntPtr.Zero, IntPtr.Zero).ToInt32();
        var start = packedSel & 0xFFFF;
        var packed = Native.SendMessage(hwnd, EmPosFromChar, (IntPtr)start, IntPtr.Zero).ToInt32();
        if (packed == -1) return false;
        var x = packed & 0xFFFF;
        var y = (packed >> 16) & 0xFFFF;
        if (x > 8000 || y > 8000) return false;
        if (!IsPointInClient(hwnd, x, y)) return false;

        var pt = new Native.Point { X = x, Y = y };
        if (!Native.ClientToScreen(hwnd, ref pt)) return false;
        if (!Native.GetClientRect(hwnd, out var client)) return false;
        var height = Math.Clamp(client.Bottom - client.Top, 12, 28);
        if (client.Bottom - client.Top > 40) height = 16;
        var rect = new Native.Rect { Left = pt.X, Top = pt.Y, Right = pt.X + 2, Bottom = pt.Y + height };
        if (!IsPlausibleScreen(rect)) return false;
        caret = ToInfo(rect, hwnd, threadId);
        return true;
    }

    private static bool IsPointInClient(IntPtr hwnd, int x, int y)
    {
        if (!Native.GetClientRect(hwnd, out var client)) return true;
        return x >= -8 && y >= -8 && x <= client.Right + 8 && y <= client.Bottom + 8;
    }

    private static bool UiaComboEditable(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var raw) && raw is ValuePattern value)
            {
                return !value.Current.IsReadOnly;
            }
        }
        catch
        {
            // some combo providers reject ValuePattern
        }

        try
        {
            return element.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)) != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNonTextControl(ControlType type) =>
        type == ControlType.Tree
        || type == ControlType.TreeItem
        || type == ControlType.List
        || type == ControlType.ListItem
        || type == ControlType.Button
        || type == ControlType.RadioButton
        || type == ControlType.CheckBox
        || type == ControlType.Hyperlink
        || type == ControlType.Tab
        || type == ControlType.TabItem
        || type == ControlType.Menu
        || type == ControlType.MenuItem
        || type == ControlType.MenuBar
        || type == ControlType.ToolBar
        || type == ControlType.Slider
        || type == ControlType.ScrollBar
        || type == ControlType.Image
        || type == ControlType.Header
        || type == ControlType.HeaderItem
        || type == ControlType.DataGrid
        || type == ControlType.DataItem
        || type == ControlType.Thumb
        || type == ControlType.SplitButton
        || type == ControlType.ProgressBar
        || type == ControlType.StatusBar
        || type == ControlType.Separator
        || type == ControlType.Calendar;

    private static bool IsTextControl(AutomationElement element)
    {
        try
        {
            var type = element.Current.ControlType;
            if (type == ControlType.Edit || type == ControlType.Document) return true;
            if (HasTextPattern(element)) return true;
            if (type == ControlType.ComboBox) return UiaComboEditable(element);
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr RealForegroundWindow()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == Native.GetDesktopWindow() || hwnd == Native.GetShellWindow())
            return IntPtr.Zero;

        if (Native.IsOurWindow(hwnd))
            return IntPtr.Zero;

        var root = Native.GetAncestor(hwnd, Native.GaRoot);
        if (root == IntPtr.Zero) root = hwnd;
        if (Native.IsIconic(root) || !Native.IsWindowVisible(root))
            return IntPtr.Zero;

        return hwnd;
    }

    private static int MarshalSize() => System.Runtime.InteropServices.Marshal.SizeOf<Native.GuiThreadInfo>();
}
