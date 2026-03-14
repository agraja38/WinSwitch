using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinSwitch;

public sealed class MouseSwipeService : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc hookProc;
    private nint hookHandle;
    private bool middleButtonDown;
    private int anchorX;
    private int lastTriggerX;
    private int triggeredSteps;
    private bool enabled = true;

    public event Action<int>? StepRequested;
    public event Action? CommitRequested;
    public event Action? CancelRequested;

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!enabled)
            {
                middleButtonDown = false;
            }
        }
    }

    public int SwipeThresholdPixels { get; set; } = 110;

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

        hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, hookProc, 0, 0);

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
        if (!Enabled)
        {
            return false;
        }

        switch (message)
        {
            case NativeMethods.WM_MBUTTONDOWN:
                middleButtonDown = true;
                anchorX = x;
                lastTriggerX = x;
                triggeredSteps = 0;
                return true;

            case NativeMethods.WM_MOUSEMOVE when middleButtonDown:
                var delta = x - lastTriggerX;
                if (Math.Abs(delta) < SwipeThresholdPixels)
                {
                    return true;
                }

                var direction = delta > 0 ? 1 : -1;
                lastTriggerX = x;
                triggeredSteps++;
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
                    if (triggeredSteps == 0)
                    {
                        StepRequested?.Invoke(x > anchorX ? 1 : -1);
                    }

                    CommitRequested?.Invoke();
                }

                return true;
        }

        return false;
    }
}
