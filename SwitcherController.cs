using System.Windows.Threading;

namespace WinSwitch;

public sealed class SwitcherController : IDisposable
{
    private readonly KeyboardHookService keyboardHookService;
    private readonly MouseSwipeService mouseSwipeService;
    private readonly TouchpadHotkeyService touchpadHotkeyService;
    private readonly WindowEnumerationService windowEnumerationService;
    private readonly ForegroundHistoryService foregroundHistoryService;
    private readonly TrayIconService trayIconService;
    private readonly GitHubReleaseUpdater updater;
    private readonly SettingsService settingsService;
    private readonly FullscreenTransitionService fullscreenTransitionService;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer swipeCommitTimer;

    private AppSettings settings;
    private IReadOnlyList<SwitchableApp> currentApps = Array.Empty<SwitchableApp>();
    private int selectedIndex = -1;
    private bool isSwitching;
    private int lastDirection = 1;

    public SwitcherController()
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        settingsService = new SettingsService();
        settings = settingsService.Load();

        windowEnumerationService = new WindowEnumerationService();
        foregroundHistoryService = new ForegroundHistoryService(windowEnumerationService.IsCandidateWindowHandle);
        keyboardHookService = new KeyboardHookService();
        mouseSwipeService = new MouseSwipeService();
        touchpadHotkeyService = new TouchpadHotkeyService();
        trayIconService = new TrayIconService();
        updater = new GitHubReleaseUpdater();
        fullscreenTransitionService = new FullscreenTransitionService();
        swipeCommitTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(settings.SwipeCommitDelayMs),
        };

        keyboardHookService.StepRequested += OnStepRequested;
        keyboardHookService.CommitRequested += OnCommitRequested;
        keyboardHookService.CancelRequested += OnCancelRequested;
        mouseSwipeService.StepRequested += OnStepRequested;
        mouseSwipeService.CommitRequested += OnCommitRequested;
        mouseSwipeService.CancelRequested += OnCancelRequested;
        touchpadHotkeyService.StepRequested += OnSwipeRequested;
        trayIconService.ShowSettingsRequested += OnShowSettingsRequested;
        trayIconService.CheckForUpdatesRequested += OnCheckForUpdatesRequested;
        trayIconService.ShowTouchpadHelpRequested += OnShowTouchpadHelpRequested;
        trayIconService.ExitRequested += OnExitRequested;
        swipeCommitTimer.Tick += OnSwipeCommitTimerTick;

        ApplySettings(settings);
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

        if (settings.CheckForUpdatesOnLaunch)
        {
            _ = updater.CheckForUpdatesAsync(
                showNoUpdateMessage: false,
                onStatusMessage: trayIconService.ShowBalloonTip,
                onError: message => trayIconService.ShowBalloonTip("Update check failed", message));
        }
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
        trayIconService.ShowSettingsRequested -= OnShowSettingsRequested;
        trayIconService.CheckForUpdatesRequested -= OnCheckForUpdatesRequested;
        trayIconService.ShowTouchpadHelpRequested -= OnShowTouchpadHelpRequested;
        trayIconService.ExitRequested -= OnExitRequested;
        swipeCommitTimer.Tick -= OnSwipeCommitTimerTick;

        keyboardHookService.Dispose();
        mouseSwipeService.Dispose();
        touchpadHotkeyService.Dispose();
        trayIconService.Dispose();
        fullscreenTransitionService.Dispose();
        foregroundHistoryService.Dispose();
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

            lastDirection = direction >= 0 ? 1 : -1;
            UpdateSelection();
        });
    }

    private void OnSwipeRequested(int direction)
    {
        OnStepRequested(direction);

        dispatcher.Invoke(() =>
        {
            swipeCommitTimer.Stop();
            swipeCommitTimer.Interval = TimeSpan.FromMilliseconds(settings.SwipeCommitDelayMs);
            swipeCommitTimer.Start();
        });
    }

    private async void OnCommitRequested()
    {
        SwitchableApp? selectedApp = null;
        nint sourceHandle = 0;

        await dispatcher.InvokeAsync(() =>
        {
            swipeCommitTimer.Stop();

            if (!isSwitching)
            {
                return;
            }

            sourceHandle = NativeMethods.GetForegroundWindow();
            selectedApp = currentApps.ElementAtOrDefault(selectedIndex);
            ResetSwitcher();
        });

        if (selectedApp is null)
        {
            return;
        }

        var targetHandle = windowEnumerationService.ResolveTargetHandle(selectedApp);
        if (targetHandle == 0)
        {
            return;
        }

        var shouldAnimate =
            !settings.RequireFullscreenForSwipe ||
            fullscreenTransitionService.CanAnimate(sourceHandle, targetHandle);

        if (shouldAnimate)
        {
            await fullscreenTransitionService.AnimateAsync(
                sourceHandle,
                targetHandle,
                () => windowEnumerationService.ActivateApp(selectedApp),
                lastDirection,
                settings.SwipeAnimationDurationMs);
        }
        else
        {
            windowEnumerationService.ActivateApp(selectedApp);
        }

        foregroundHistoryService.RecordProcess(selectedApp.ProcessId);
    }

    private void OnCancelRequested()
    {
        dispatcher.Invoke(() =>
        {
            swipeCommitTimer.Stop();
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
            "Open Windows Settings > Bluetooth & devices > Touchpad > Advanced gestures and map three-finger left/right to Ctrl+Alt+Left and Ctrl+Alt+Right.";

        trayIconService.ShowMessage("Touchpad setup", message);
    }

    private void OnShowSettingsRequested()
    {
        dispatcher.Invoke(() =>
        {
            var window = new SettingsWindow(settingsService);
            window.LoadSettings(settings);
            window.ManualUpdateRequested += OnCheckForUpdatesRequested;
            window.SettingsSaved += updatedSettings =>
            {
                settings = updatedSettings;
                ApplySettings(updatedSettings);
                trayIconService.ShowBalloonTip("Settings saved", "WinSwitch updated your preferences.");
            };
            window.ShowDialog();
        });
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

    private void ApplySettings(AppSettings updatedSettings)
    {
        keyboardHookService.Enabled = updatedSettings.EnableAltTab;
        mouseSwipeService.Enabled = updatedSettings.EnableMouseSwipe;
        mouseSwipeService.SwipeThresholdPixels = updatedSettings.MouseSwipeThreshold;
        touchpadHotkeyService.Enabled = updatedSettings.EnableTouchpadSwipe;
        swipeCommitTimer.Interval = TimeSpan.FromMilliseconds(updatedSettings.SwipeCommitDelayMs);
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
