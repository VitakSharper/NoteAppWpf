using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace NoteApp.Views;

// NoteApp's icon in the notification area: open the window, write a quick note, quit — and
// the notifications (reminders, "still running"), which Windows 10/11 shows as toasts.
// Created by App.xaml.cs only, so no test ever puts an icon in the real notification area.
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private Action? _onNotificationClick;

    public event Action? OpenRequested;
    public event Action? QuickNoteRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open NoteApp", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Quick note (Ctrl+Alt+N)", null, (_, _) => QuickNoteRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new Forms.NotifyIcon
        {
            Icon = AppIcon(),
            Text = "NoteApp",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.BalloonTipClicked += (_, _) => _onNotificationClick?.Invoke();
    }

    // onClick: what a click on the notification does (a reminder opens its note).
    public void Notify(string title, string text, Action? onClick = null)
    {
        _onNotificationClick = onClick;
        _icon.ShowBalloonTip(10_000, title, text, Forms.ToolTipIcon.Info);
    }

    // The exe's own icon (ApplicationIcon), so the tray shows what the taskbar shows.
    private static Drawing.Icon AppIcon() =>
        (Environment.ProcessPath is { } exe ? Drawing.Icon.ExtractAssociatedIcon(exe) : null) ?? Drawing.SystemIcons.Application;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
