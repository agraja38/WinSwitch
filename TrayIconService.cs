using System.Drawing;
using System.Windows.Forms;

namespace WinSwitch;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;

    public event Action? CheckForUpdatesRequested;
    public event Action? ShowTouchpadHelpRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        menu = new ContextMenuStrip();
        menu.Items.Add("Check for updates", null, (_, _) => CheckForUpdatesRequested?.Invoke());
        menu.Items.Add("Touchpad setup", null, (_, _) => ShowTouchpadHelpRequested?.Invoke());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = "WinSwitch",
            Visible = false,
            ContextMenuStrip = menu,
        };

        notifyIcon.DoubleClick += (_, _) => ShowTouchpadHelpRequested?.Invoke();
    }

    public void Start()
    {
        notifyIcon.Visible = true;
        ShowBalloonTip("WinSwitch is running", "Use Alt+Tab, middle-button swipe, or your mapped three-finger swipe.");
    }

    public void ShowBalloonTip(string title, string message)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.ShowBalloonTip(4000);
    }

    public void ShowMessage(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
    }
}
