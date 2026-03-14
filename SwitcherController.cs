using System.Windows.Threading;

namespace WinSwitch;

public sealed class SwitcherController : IDisposable
{
    private readonly KeyboardHookService keyboardHookService;
    private readonly MouseSwipeService mouseSwipeService;
    private readonly TouchpadHotkeyService touchpadHotkeyService;
    private readonly WindowEnumerationService windowEnumerationService;
    private readonly ForegroundHistoryService foregroundHistoryService;
    private readonly SwitcherOverlayWindow overlayWindow;
    private readonly TrayIconService trayIconService;
    private readonly GitHubReleaseUpdater updater;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer swipeCommitTimer;

    private IReadOnlyList<SwitchableApp> currentApps = Array.Empty<SwitchableApp>();
    private int selectedIndex = -1;
    private bool isSwitching;

    public SwitcherController()
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        windowEnumerationService = new WindowEnumerationService();
        foregroundHistoryService = new ForegroundHistoryService(windowEnumerationService.IsCandidateWindowHandle);
        keyboardHookService = new KeyboardHookService();
        mouseSwipeService = new MouseSwipeService();
        touchpadHotkeyService = new TouchpadHotkeyService();
        overlayWindow = new SwitcherOverlayWindow();
        trayIconService = new TrayIconService();
        updater = new GitHubReleaseUpdater();
        swipeCommitTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(650),
        };

        keyboardHookService.StepRequested += OnStepRequested;
        keyboardHookService.CommitRequested += OnCommitRequested;
        keyboardHookService.CancelRequested += OnCancelRequested;
        mouseSwipeService.StepRequested += OnStepRequested;
        mouseSwipeService.CommitRequested += OnCommitRequested;
        mouseSwipeService.CancelRequested += OnCancelRequested;
        touchpadHotkeyService.StepRequested += OnSwipeRequested;
        trayIconService.CheckForUpdatesRequested += OnCheckForUpdatesRequested;
        trayIconService.ShowTouchpadHelpRequested += OnShowTouchpadHelpRequested;
        trayIconService.ExitRequested += OnExitRequested;
        swipeCommitTimer.Tick += OnSwipeCommitTimerTick;
    }

    public void Start()
    {
        foregroundHistoryService.Start();
        keyboardHookService.Start();
        mouseSwipeService.Start();
        trayIconService.Start();

        try
        {
            touchpadHotkeyService.Start();
        }
        catch (Exception ex)
        {
            trayIconService.ShowBalloonTip("Touchpad hotkeys unavailable", ex.Message);
        }

        _ = updater.CheckForUpdatesAsync(
            showNoUpdateMessage: false,
            onStatusMessage: trayIconService.ShowBalloonTip,
            onError: message => trayIconService.ShowBalloonTip("Update check failed", message));
    }

    public void Dispose()
    {
        keyboardHookService.StepRequested -= OnStepRequested;
        keyboardHookService.CommitRequested -= OnCommitRequested;
        keyboardHookService.CancelRequested -= OnCancelRequested;
        mouseSwipeService.StepRequested -= OnStepRequested;
        mouseSwipeService.CommitRequested -= OnCommitRequested;
        mouseSwipeService.CancelRequested -= OnCancelRequested;
        touchpadHotkeyService.StepRequested -= OnSwipeRequested;
        trayIconService.CheckForUpdatesRequested -= OnCheckForUpdatesRequested;
        trayIconService.ShowTouchpadHelpRequested -= OnShowTouchpadHelpRequested;
        trayIconService.ExitRequested -= OnExitRequested;
        swipeCommitTimer.Tick -= OnSwipeCommitTimerTick;

        keyboardHookService.Dispose();
        mouseSwipeService.Dispose();
        touchpadHotkeyService.Dispose();
        trayIconService.Dispose();
        foregroundHistoryService.Dispose();
        overlayWindow.Close();
    }

    private void OnStepRequested(int direction)
    {
        dispatcher.Invoke(() =>
        {
            if (!isSwitching)
            {
                currentApps = foregroundHistoryService.OrderApps(windowEnumerationService.GetOpenApps());
                if (currentApps.Count == 0)
                {
                    return;
                }

                isSwitching = true;
                selectedIndex = currentApps.Count == 1 ? 0 : (direction > 0 ? 1 : currentApps.Count - 1);
            }
            else
            {
                selectedIndex = Wrap(selectedIndex + direction, currentApps.Count);
            }

            UpdateSelection();
            overlayWindow.ShowSwitcher(currentApps, selectedIndex);
        });
    }

    private void OnSwipeRequested(int direction)
    {
        OnStepRequested(direction);

        dispatcher.Invoke(() =>
        {
            swipeCommitTimer.Stop();
            swipeCommitTimer.Start();
        });
    }

    private void OnCommitRequested()
    {
        dispatcher.Invoke(() =>
        {
            swipeCommitTimer.Stop();

            if (!isSwitching)
            {
                return;
            }

            var selectedApp = currentApps.ElementAtOrDefault(selectedIndex);
            ResetSwitcher();

            if (selectedApp is null)
            {
                return;
            }

            windowEnumerationService.ActivateApp(selectedApp);
            foregroundHistoryService.RecordProcess(selectedApp.ProcessId);
        });
    }

    private void OnCancelRequested()
    {
        dispatcher.Invoke(() =>
        {
            swipeCommitTimer.Stop();

            if (!isSwitching)
            {
                return;
            }

            ResetSwitcher();
        });
    }

    private async void OnCheckForUpdatesRequested()
    {
        try
        {
            await updater.CheckForUpdatesAsync(
                showNoUpdateMessage: true,
                onStatusMessage: trayIconService.ShowBalloonTip,
                onError: message => trayIconService.ShowBalloonTip("Update check failed", message));
        }
        catch (Exception ex)
        {
            trayIconService.ShowBalloonTip("Update check failed", ex.Message);
        }
    }

    private void OnShowTouchpadHelpRequested()
    {
        const string message =
            "To use three-finger swipes with WinSwitch, open Windows Settings > Bluetooth & devices > Touchpad > Advanced gestures " +
            "and map the three-finger swipe left/right actions to Ctrl+Alt+Left and Ctrl+Alt+Right. " +
            "WinSwitch listens for those global shortcuts and commits the selected app automatically after the swipe.";

        trayIconService.ShowMessage("Enable touchpad swipe", message);
    }

    private void OnExitRequested()
    {
        dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
    }

    private void OnSwipeCommitTimerTick(object? sender, EventArgs e)
    {
        swipeCommitTimer.Stop();
        OnCommitRequested();
    }

    private void ResetSwitcher()
    {
        overlayWindow.HideSwitcher();
        currentApps = Array.Empty<SwitchableApp>();
        selectedIndex = -1;
        isSwitching = false;
    }

    private void UpdateSelection()
    {
        for (var index = 0; index < currentApps.Count; index++)
        {
            currentApps[index].IsSelected = index == selectedIndex;
        }
    }

    private static int Wrap(int value, int count)
    {
        if (count == 0)
        {
            return -1;
        }

        var remainder = value % count;
        return remainder < 0 ? remainder + count : remainder;
    }
}
