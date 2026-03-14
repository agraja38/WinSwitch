using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinSwitch;

public sealed class WindowEnumerationService
{
    private readonly int currentProcessId = Environment.ProcessId;

    public IReadOnlyList<SwitchableApp> GetOpenApps()
    {
        var apps = new List<SwitchableApp>();
        var seenProcesses = new HashSet<int>();

        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!IsCandidateWindowHandle(windowHandle))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(windowHandle, out var processIdValue);
            var processId = (int)processIdValue;

            if (processId == currentProcessId || !seenProcesses.Add(processId))
            {
                return true;
            }

            var app = CreateApp(windowHandle, processId);
            if (app is not null)
            {
                apps.Add(app);
            }

            return true;
        }, 0);

        return apps;
    }

    public bool IsCandidateWindowHandle(nint windowHandle)
    {
        if (windowHandle == 0 || !NativeMethods.IsWindowVisible(windowHandle))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if ((int)processId == currentProcessId)
        {
            return false;
        }

        if (NativeMethods.GetWindow(windowHandle, NativeMethods.GW_OWNER) != 0)
        {
            return false;
        }

        var extendedStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GWL_EXSTYLE).ToInt64();
        if ((extendedStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (IsCloaked(windowHandle))
        {
            return false;
        }

        return NativeMethods.GetWindowTextLength(windowHandle) > 0;
    }

    public void ActivateApp(SwitchableApp app)
    {
        var targetHandle = FindBestWindowForProcess(app.ProcessId) ?? app.WindowHandle;
        if (targetHandle == 0)
        {
            return;
        }

        if (NativeMethods.IsIconic(targetHandle))
        {
            NativeMethods.ShowWindow(targetHandle, NativeMethods.SW_RESTORE);
        }
        else
        {
            NativeMethods.ShowWindow(targetHandle, NativeMethods.SW_SHOW);
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var foregroundThread = foregroundWindow == 0 ? 0u : NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThread = NativeMethods.GetWindowThreadProcessId(targetHandle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        try
        {
            if (foregroundThread != 0)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
                NativeMethods.AttachThreadInput(targetThread, foregroundThread, true);
            }

            NativeMethods.BringWindowToTop(targetHandle);
            NativeMethods.SetForegroundWindow(targetHandle);
            NativeMethods.SetActiveWindow(targetHandle);
            NativeMethods.SetFocus(targetHandle);
        }
        finally
        {
            if (foregroundThread != 0)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
                NativeMethods.AttachThreadInput(targetThread, foregroundThread, false);
            }
        }
    }

    private SwitchableApp? CreateApp(nint windowHandle, int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var executablePath = TryGetExecutablePath(process);
            var displayName = GetDisplayName(process, executablePath);
            var windowTitle = GetWindowTitle(windowHandle);
            var icon = GetIcon(executablePath);

            return new SwitchableApp
            {
                ProcessId = processId,
                DisplayName = displayName,
                WindowTitle = windowTitle,
                WindowHandle = windowHandle,
                Icon = icon,
                ExecutablePath = executablePath,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetWindowTitle(nint windowHandle)
    {
        var length = NativeMethods.GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(windowHandle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Win32Exception)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static string GetDisplayName(Process process, string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(executablePath);
                if (!string.IsNullOrWhiteSpace(version.FileDescription))
                {
                    return version.FileDescription;
                }
            }
            catch
            {
            }
        }

        return string.IsNullOrWhiteSpace(process.ProcessName) ? "Unknown App" : process.ProcessName;
    }

    private static ImageSource? GetIcon(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        var fileInfo = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(
            executablePath,
            0,
            ref fileInfo,
            (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

        if (result == 0 || fileInfo.hIcon == 0)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(48, 48));
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DestroyIcon(fileInfo.hIcon);
        }
    }

    private bool IsCloaked(nint windowHandle)
    {
        if (NativeMethods.DwmGetWindowAttribute(
            windowHandle,
            NativeMethods.DWMWA_CLOAKED,
            out var cloaked,
            Marshal.SizeOf<int>()) != 0)
        {
            return false;
        }

        return cloaked != 0;
    }

    private nint? FindBestWindowForProcess(int processId)
    {
        nint result = 0;

        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!IsCandidateWindowHandle(windowHandle))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(windowHandle, out var candidateProcessId);
            if ((int)candidateProcessId != processId)
            {
                return true;
            }

            result = windowHandle;
            return false;
        }, 0);

        return result == 0 ? null : result;
    }
}
