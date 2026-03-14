using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinSwitch;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly Icon appIcon;

    public event Action? CheckForUpdatesRequested;
    public event Action? ShowTouchpadHelpRequested;
    public event Action? ShowSettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => ShowSettingsRequested?.Invoke());
        menu.Items.Add("Check for updates", null, (_, _) => CheckForUpdatesRequested?.Invoke());
        menu.Items.Add("Touchpad setup", null, (_, _) => ShowTouchpadHelpRequested?.Invoke());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        appIcon = LoadAppIcon();

        notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "WinSwitch - Created by Agraja",
            Visible = false,
            ContextMenuStrip = menu,
        };

        notifyIcon.DoubleClick += (_, _) => ShowSettingsRequested?.Invoke();
    }

    public void Start()
    {
        notifyIcon.Visible = true;
        ShowBalloonTip("WinSwitch is running", "Created by Agraja. Use Ctrl+Alt+Left/Right or your mapped three-finger swipe.");
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
        appIcon.Dispose();
        menu.Dispose();
    }

    private static Icon LoadAppIcon()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AppIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "assets", "AppIcon.ico"),
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return new Icon(path);
            }
        }

        return Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
    }
}
