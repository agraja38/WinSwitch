using System.Diagnostics;
using System.Windows;

namespace WinSwitch;

public partial class SettingsWindow : Window
{
    private readonly SettingsService settingsService;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        this.settingsService = settingsService;

        ThresholdSlider.ValueChanged += (_, _) => ThresholdValueText.Text = $"Swipe threshold: {(int)ThresholdSlider.Value}px";
        CommitDelaySlider.ValueChanged += (_, _) => CommitDelayValueText.Text = $"Commit after {(int)CommitDelaySlider.Value} ms";
        AnimationDurationSlider.ValueChanged += (_, _) => AnimationDurationValueText.Text = $"Animation length {(int)AnimationDurationSlider.Value} ms";
        SaveButton.Click += OnSaveClicked;
        OpenSettingsFileButton.Click += OnOpenSettingsFileClicked;
    }

    public event Action<AppSettings>? SettingsSaved;

    public void LoadSettings(AppSettings settings)
    {
        AltTabCheckBox.IsChecked = settings.EnableAltTab;
        MouseSwipeCheckBox.IsChecked = settings.EnableMouseSwipe;
        TouchpadSwipeCheckBox.IsChecked = settings.EnableTouchpadSwipe;
        RequireFullscreenCheckBox.IsChecked = settings.RequireFullscreenForSwipe;
        CheckForUpdatesCheckBox.IsChecked = settings.CheckForUpdatesOnLaunch;
        ThresholdSlider.Value = settings.MouseSwipeThreshold;
        CommitDelaySlider.Value = settings.SwipeCommitDelayMs;
        AnimationDurationSlider.Value = settings.SwipeAnimationDurationMs;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            EnableAltTab = AltTabCheckBox.IsChecked == true,
            EnableMouseSwipe = MouseSwipeCheckBox.IsChecked == true,
            EnableTouchpadSwipe = TouchpadSwipeCheckBox.IsChecked == true,
            RequireFullscreenForSwipe = RequireFullscreenCheckBox.IsChecked == true,
            CheckForUpdatesOnLaunch = CheckForUpdatesCheckBox.IsChecked == true,
            MouseSwipeThreshold = (int)ThresholdSlider.Value,
            SwipeCommitDelayMs = (int)CommitDelaySlider.Value,
            SwipeAnimationDurationMs = AnimationDurationSlider.Value,
        };

        settingsService.Save(settings);
        SettingsSaved?.Invoke(settings);
        Close();
    }

    private void OnOpenSettingsFileClicked(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{settingsService.SettingsPath}\"",
            UseShellExecute = true,
        });
    }
}
