namespace WinSwitch;

public sealed class AppSettings
{
    public bool EnableAltTab { get; set; } = true;
    public bool EnableTouchpadSwipe { get; set; } = true;
    public bool RequireFullscreenForSwipe { get; set; } = true;
    public bool CheckForUpdatesOnLaunch { get; set; } = true;
    public int SwipeCommitDelayMs { get; set; } = 260;
    public double SwipeAnimationDurationMs { get; set; } = 240;
}
