using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinSwitch;

public sealed class MouseSwipeService : IDisposable
{
    private const int SwipeThresholdPixels = 110;

    private readonly NativeMethods.LowLevelMouseProc hookProc;
    private nint hookHandle;
    private bool middleButtonDown;
    private int anchorX;
    private int lastTriggerX;

    public event Action<int>? StepRequested;
    public event Action? CommitRequested;
    public event Action? CancelRequested;

    public MouseSwipeService()
    {
        hookProc = HookCallback;
    }

    public void Start()
    {
        if (hookHandle != 0)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);
        hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, hookProc, moduleHandle, 0);

        if (hookHandle == 0)
        {
            throw new InvalidOperationException("Unable to install the low-level mouse hook.");
        }
    }

    public void Dispose()
    {
        if (hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(hookHandle);
            hookHandle = 0;
        }
    }

    private nint HookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
        var isHandled = HandleMouseMessage((uint)wParam, data.pt.X);
        return isHandled ? 1 : NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private bool HandleMouseMessage(uint message, int x)
    {
        switch (message)
        {
            case NativeMethods.WM_MBUTTONDOWN:
                middleButtonDown = true;
                anchorX = x;
                lastTriggerX = x;
                return true;

            case NativeMethods.WM_MOUSEMOVE when middleButtonDown:
                var delta = x - lastTriggerX;
                if (Math.Abs(delta) < SwipeThresholdPixels)
                {
                    return true;
                }

                var direction = delta > 0 ? 1 : -1;
                lastTriggerX = x;
                StepRequested?.Invoke(direction);
                return true;

            case NativeMethods.WM_MBUTTONUP:
                if (!middleButtonDown)
                {
                    return false;
                }

                middleButtonDown = false;

                if (Math.Abs(x - anchorX) < SwipeThresholdPixels)
                {
                    CancelRequested?.Invoke();
                }
                else
                {
                    CommitRequested?.Invoke();
                }

                return true;
        }

        return false;
    }
}
