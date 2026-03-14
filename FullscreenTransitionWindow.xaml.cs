using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WinSwitch;

public partial class FullscreenTransitionWindow : Window
{
    public FullscreenTransitionWindow()
    {
        InitializeComponent();
        Hide();
    }

    public Task PlayAsync(BitmapSource currentFrame, BitmapSource targetFrame, double left, double top, double width, double height, int direction, double durationMs)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        RootGrid.Width = width;
        RootGrid.Height = height;
        ImageCanvas.Width = width * 2;
        ImageCanvas.Height = height;

        CurrentImage.Width = width;
        CurrentImage.Height = height;
        CurrentImage.Source = currentFrame;
        System.Windows.Controls.Canvas.SetLeft(CurrentImage, direction > 0 ? 0 : width);

        TargetImage.Width = width;
        TargetImage.Height = height;
        TargetImage.Source = targetFrame;
        System.Windows.Controls.Canvas.SetLeft(TargetImage, direction > 0 ? width : 0);

        SlideTransform.X = direction > 0 ? 0 : -width;

        if (!IsVisible)
        {
            Show();
        }

        var animation = new DoubleAnimation
        {
            From = direction > 0 ? 0 : -width,
            To = direction > 0 ? -width : 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        animation.Completed += (_, _) =>
        {
            Hide();
            completion.TrySetResult();
        };

        SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
        return completion.Task;
    }
}
