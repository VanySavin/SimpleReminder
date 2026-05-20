using System.Drawing;
using System.Windows.Forms;

namespace ReminderApp.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;

    public event EventHandler? OpenClicked;
    public event EventHandler? TestNotificationClicked;
    public event EventHandler? ExitClicked;

    public TrayService()
    {
        _trayIcon = AppIconLoader.GetNotifyIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Visible = true,
            Text = "SimpleReminder"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => OpenClicked?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Тест уведомления", null, (_, _) => TestNotificationClicked?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Выход", null, (_, _) => ExitClicked?.Invoke(this, EventArgs.Empty));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => OpenClicked?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon = null;
        _notifyIcon.Dispose();
        if (AppIconLoader.ShouldDisposeNotifyIcon(_trayIcon))
        {
            _trayIcon.Dispose();
        }
    }
}
