using System.Windows.Threading;

namespace WinSwitch;

public sealed class SwitcherController : IDisposable
{
    private readonly TouchpadHotkeyService touchpadHotkeyService;
    private readonly WindowEnumerationService windowEnumerationService;
    private readonly ForegroundHistoryService foregroundHistoryService;
    private readonly TrayIconService trayIconService;
    private readonly GitHubReleaseUpdater updater;
    private readonly SettingsService settingsService;
    private readonly FullscreenTransitionService fullscreenTransitionService;
    private readonly UpdateProgressWindow updateProgressWindow;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer swipeCommitTimer;

    private AppSettings settings;
    private SettingsWindow? settingsWindow;
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
        touchpadHotkeyService = new TouchpadHotkeyService();
        trayIconService = new TrayIconService();
        updater = new GitHubReleaseUpdater();
        fullscreenTransitionService = new FullscreenTransitionService();
        updateProgressWindow = new UpdateProgressWindow();
        swipeCommitTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(settings.SwipeCommitDelayMs),
        };

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
        trayIconService.Start();

        try
        {
            touchpadHotkeyService.Start();
        }
        catch (Exception ex)
        {
            trayIconService.ShowBalloonTip("Shortcut hotkeys unavailable", ex.Message);
        }

        if (settings.CheckForUpdatesOnLaunch)
        {
            _ = updater.CheckForUpdatesAsync(
                showNoUpdateMessage: false,
                onStatusMessage: trayIconService.ShowBalloonTip,
                onError: message => trayIconService.ShowBalloonTip("Update check failed", message),
                onProgress: ShowUpdateProgress,
                beforeInstall: PrepareForUpdateAsync);
        }
    }

    public void Dispose()
    {
        touchpadHotkeyService.StepRequested -= OnSwipeRequested;
        trayIconService.ShowSettingsRequested -= OnShowSettingsRequested;
        trayIconService.CheckForUpdatesRequested -= OnCheckForUpdatesRequested;
        trayIconService.ShowTouchpadHelpRequested -= OnShowTouchpadHelpRequested;
        trayIconService.ExitRequested -= OnExitRequested;
        swipeCommitTimer.Tick -= OnSwipeCommitTimerTick;

        touchpadHotkeyService.Dispose();
        trayIconService.Dispose();
        fullscreenTransitionService.Dispose();
        updateProgressWindow.Close();
        foregroundHistoryService.Dispose();
    }

    private void OnSwipeRequested(int direction)
    {
        dispatcher.BeginInvoke(() =>
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
            swipeCommitTimer.Stop();
            swipeCommitTimer.Interval = TimeSpan.FromMilliseconds(settings.SwipeCommitDelayMs);
            swipeCommitTimer.Start();
        }, DispatcherPriority.Send);
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

    private async void OnCheckForUpdatesRequested()
    {
        try
        {
            await updater.CheckForUpdatesAsync(
                showNoUpdateMessage: true,
                onStatusMessage: trayIconService.ShowBalloonTip,
                onError: message => trayIconService.ShowBalloonTip("Update check failed", message),
                onProgress: ShowUpdateProgress,
                beforeInstall: PrepareForUpdateAsync);
        }
        catch (Exception ex)
        {
            trayIconService.ShowBalloonTip("Update check failed", ex.Message);
        }
    }

    private void OnShowTouchpadHelpRequested()
    {
        const string message =
            "Use Ctrl+Alt+Left and Ctrl+Alt+Right for keyboard switching. In Windows touchpad advanced gestures, map three-finger left/right to the same shortcuts.";

        trayIconService.ShowMessage("Shortcuts - Created by Agraja", message);
    }

    private void OnShowSettingsRequested()
    {
        dispatcher.Invoke(() =>
        {
            if (settingsWindow is not null)
            {
                if (!settingsWindow.IsVisible)
                {
                    settingsWindow.Show();
                }

                settingsWindow.WindowState = System.Windows.WindowState.Normal;
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new SettingsWindow(settingsService);
            settingsWindow.LoadSettings(settings);
            settingsWindow.ManualUpdateRequested += OnCheckForUpdatesRequested;
            settingsWindow.SettingsSaved += updatedSettings =>
            {
                settings = updatedSettings;
                ApplySettings(updatedSettings);
                trayIconService.ShowBalloonTip("Settings saved", "WinSwitch updated your preferences. Created by Agraja.");
            };
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            settingsWindow.Show();
            settingsWindow.Activate();
        });
    }

    public void ShowSettings()
    {
        OnShowSettingsRequested();
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
        touchpadHotkeyService.Enabled = updatedSettings.EnableKeyboardShortcuts || updatedSettings.EnableTouchpadSwipe;
        swipeCommitTimer.Interval = TimeSpan.FromMilliseconds(updatedSettings.SwipeCommitDelayMs);
    }

    private Task PrepareForUpdateAsync()
    {
        return dispatcher.InvokeAsync(() =>
        {
            swipeCommitTimer.Stop();
            ResetSwitcher();

            if (settingsWindow is not null)
            {
                settingsWindow.Close();
                settingsWindow = null;
            }

            touchpadHotkeyService.Enabled = false;
            touchpadHotkeyService.Dispose();
            updateProgressWindow.ShowIndeterminate("Installing update...");
        }).Task;
    }

    private void ShowUpdateProgress(string status, double? percent, bool isIndeterminate)
    {
        dispatcher.BeginInvoke(() =>
        {
            if (!updateProgressWindow.IsVisible)
            {
                updateProgressWindow.Show();
            }

            updateProgressWindow.WindowState = System.Windows.WindowState.Normal;
            updateProgressWindow.Activate();

            if (isIndeterminate)
            {
                updateProgressWindow.ShowIndeterminate(status);
            }
            else
            {
                updateProgressWindow.ShowProgress(status, percent ?? 0);
            }
        }, DispatcherPriority.Background);
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
