using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinSwitch;

public sealed class KeyboardHookService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc hookProc;
    private nint hookHandle;
    private bool leftAltDown;
    private bool rightAltDown;
    private bool leftShiftDown;
    private bool rightShiftDown;
    private bool sequenceActive;
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
                sequenceActive = false;
            }
        }
    }

    public KeyboardHookService()
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
        hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, hookProc, moduleHandle, 0);

        if (hookHandle == 0)
        {
            throw new InvalidOperationException("Unable to install the low-level keyboard hook.");
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

        var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        var isHandled = HandleKeyMessage((uint)wParam, (int)data.vkCode);
        return isHandled ? 1 : NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private bool HandleKeyMessage(uint message, int virtualKey)
    {
        if (!Enabled)
        {
            return false;
        }

        var isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        var isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

        switch (virtualKey)
        {
            case NativeMethods.VK_LMENU:
                leftAltDown = isDown || (leftAltDown && !isUp);
                return HandleAltRelease(isUp);
            case NativeMethods.VK_RMENU:
                rightAltDown = isDown || (rightAltDown && !isUp);
                return HandleAltRelease(isUp);
            case NativeMethods.VK_LSHIFT:
                leftShiftDown = isDown || (leftShiftDown && !isUp);
                break;
            case NativeMethods.VK_RSHIFT:
                rightShiftDown = isDown || (rightShiftDown && !isUp);
                break;
            case NativeMethods.VK_ESCAPE when isDown && sequenceActive:
                sequenceActive = false;
                CancelRequested?.Invoke();
                return true;
            case NativeMethods.VK_TAB when isDown && IsAltDown:
                sequenceActive = true;
                StepRequested?.Invoke(IsShiftDown ? -1 : 1);
                return true;
        }

        return sequenceActive && virtualKey == NativeMethods.VK_TAB;
    }

    private bool HandleAltRelease(bool isUp)
    {
        if (!isUp)
        {
            return false;
        }

        if (!IsAltDown && sequenceActive)
        {
            sequenceActive = false;
            CommitRequested?.Invoke();
            return true;
        }

        return sequenceActive;
    }

    private bool IsAltDown => leftAltDown || rightAltDown;

    private bool IsShiftDown => leftShiftDown || rightShiftDown;
}
