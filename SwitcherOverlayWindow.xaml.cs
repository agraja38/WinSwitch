using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace WinSwitch;

public partial class SwitcherOverlayWindow : Window
{
    private readonly ObservableCollection<SwitcherOverlayItem> items = [];

    public SwitcherOverlayWindow()
    {
        InitializeComponent();
        AppItemsControl.ItemsSource = items;
        Hide();
    }

    public void ShowSwitcher(IReadOnlyList<SwitchableApp> apps, int selectedIndex)
    {
        items.Clear();

        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            var isSelected = index == selectedIndex;

            items.Add(new SwitcherOverlayItem
            {
                DisplayName = app.DisplayName,
                Subtitle = string.IsNullOrWhiteSpace(app.WindowTitle) ? app.ExecutablePath : app.WindowTitle,
                Icon = app.Icon,
                CardBackground = isSelected ? SelectedBackgroundBrush : DefaultBackgroundBrush,
                CardBorderBrush = isSelected ? SelectedBorderBrush : DefaultBorderBrush,
            });
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideSwitcher()
    {
        Hide();
        items.Clear();
    }

    private static System.Windows.Media.Brush DefaultBackgroundBrush => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1AFFFFFF"));
    private static System.Windows.Media.Brush SelectedBackgroundBrush => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F29D38"));
    private static System.Windows.Media.Brush DefaultBorderBrush => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FFFFFF"));
    private static System.Windows.Media.Brush SelectedBorderBrush => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF8D58A"));
}

public sealed class SwitcherOverlayItem
{
    public required string DisplayName { get; init; }
    public required string Subtitle { get; init; }
    public required ImageSource? Icon { get; init; }
    public required System.Windows.Media.Brush CardBackground { get; init; }
    public required System.Windows.Media.Brush CardBorderBrush { get; init; }
}
