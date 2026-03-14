using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WinSwitch;

public sealed class FullscreenTransitionService : IDisposable
{
    private const int FullscreenTolerance = 12;

    private readonly FullscreenTransitionWindow transitionWindow;

    public FullscreenTransitionService()
    {
        transitionWindow = new FullscreenTransitionWindow();
    }

    public bool CanAnimate(nint sourceHandle, nint targetHandle)
    {
        return TryGetFullscreenBounds(sourceHandle, out _) && TryGetFullscreenBounds(targetHandle, out _);
    }

    public async Task AnimateAsync(nint sourceHandle, nint targetHandle, Action activateTarget, int direction, double durationMs)
    {
        if (!TryGetFullscreenBounds(sourceHandle, out var bounds))
        {
            activateTarget();
            return;
        }

        using var currentFrame = CaptureScreen(bounds);
        activateTarget();
        await Task.Delay(90);

        if (!TryGetFullscreenBounds(targetHandle, out var targetBounds))
        {
            return;
        }

        using var targetFrame = CaptureScreen(targetBounds);
        var currentSource = ConvertBitmap(currentFrame);
        var targetSource = ConvertBitmap(targetFrame);

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            transitionWindow.PlayAsync(
                currentSource,
                targetSource,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                direction,
                durationMs)).Task.Unwrap();
    }

    public void Dispose()
    {
        transitionWindow.Close();
    }

    private static bool TryGetFullscreenBounds(nint windowHandle, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (windowHandle == 0 || !NativeMethods.GetWindowRect(windowHandle, out var rect))
        {
            return false;
        }

        var screen = Screen.FromHandle(windowHandle);
        var windowBounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var screenBounds = screen.Bounds;

        var coversScreen =
            Math.Abs(windowBounds.Left - screenBounds.Left) <= FullscreenTolerance &&
            Math.Abs(windowBounds.Top - screenBounds.Top) <= FullscreenTolerance &&
            Math.Abs(windowBounds.Right - screenBounds.Right) <= FullscreenTolerance &&
            Math.Abs(windowBounds.Bottom - screenBounds.Bottom) <= FullscreenTolerance;

        if (!coversScreen)
        {
            return false;
        }

        bounds = screenBounds;
        return true;
    }

    private static Bitmap CaptureScreen(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static BitmapSource ConvertBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();

        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(handle);
        }
    }
}
