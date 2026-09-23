using System.Drawing;
using System.Windows.Forms;

namespace CursorUI;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayService(Action openSettings, Action shutdown)
    {
        _icon = new NotifyIcon
        {
            Visible = false,
            Text = "CursorUI",
            Icon = LoadIcon()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Настройки", null, (_, _) => openSettings());
        menu.Items.Add("Выход", null, (_, _) => shutdown());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => openSettings();
    }

    public void Show() => _icon.Visible = true;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static Icon LoadIcon()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            try
            {
                var extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted != null) return extracted;
            }
            catch
            {
                // fall back
            }
        }

        return SystemIcons.Application;
    }
}
