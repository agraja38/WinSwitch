using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinSwitch;

public sealed class TouchpadHotkeyService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc hookProc;
    private nint hookHandle;
    private bool enabled = true;
    private bool leftControlDown;
    private bool rightControlDown;
    private bool leftAltDown;
    private bool rightAltDown;
    private bool leftArrowDown;
    private bool rightArrowDown;

    public event Action<int>? StepRequested;

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!enabled)
            {
                ResetKeyState();
            }
        }
    }

    public bool EnableKeyboardShortcuts
    {
        get => enabled;
        set => Enabled = value;
    }

    public TouchpadHotkeyService()
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
            throw new InvalidOperationException("Unable to install the shortcut keyboard hook.");
        }
    }

    public void Dispose()
    {
        ResetKeyState();

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

        try
        {
            var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var handled = HandleKeyMessage((uint)wParam, (int)data.vkCode, data.flags);
            return handled ? 1 : NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }
        catch
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }
    }

    private bool HandleKeyMessage(uint message, int virtualKey, uint flags)
    {
        var isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        var isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

        switch (virtualKey)
        {
            case NativeMethods.VK_CONTROL:
            case NativeMethods.VK_LCONTROL:
                leftControlDown = isDown || (leftControlDown && !isUp);
                return false;
            case NativeMethods.VK_RCONTROL:
                rightControlDown = isDown || (rightControlDown && !isUp);
                return false;
            case NativeMethods.VK_MENU:
            case NativeMethods.VK_LMENU:
                leftAltDown = isDown || (leftAltDown && !isUp);
                return false;
            case NativeMethods.VK_RMENU:
                rightAltDown = isDown || (rightAltDown && !isUp);
                return false;
            case NativeMethods.VK_LEFT:
                if (isUp)
                {
                    leftArrowDown = false;
                    return false;
                }

                if (!Enabled || leftArrowDown || !IsControlDown || !IsAltDown)
                {
                    return false;
                }

                leftArrowDown = true;
                StepRequested?.Invoke(-1);
                return true;
            case NativeMethods.VK_RIGHT:
                if (isUp)
                {
                    rightArrowDown = false;
                    return false;
                }

                if (!Enabled || rightArrowDown || !IsControlDown || !IsAltDown)
                {
                    return false;
                }

                rightArrowDown = true;
                StepRequested?.Invoke(1);
                return true;
        }

        if ((flags & NativeMethods.LLKHF_ALTDOWN) == 0 && isUp && virtualKey is NativeMethods.VK_LEFT or NativeMethods.VK_RIGHT)
        {
            leftArrowDown = false;
            rightArrowDown = false;
        }

        return false;
    }

    private void ResetKeyState()
    {
        leftControlDown = false;
        rightControlDown = false;
        leftAltDown = false;
        rightAltDown = false;
        leftArrowDown = false;
        rightArrowDown = false;
    }

    private bool IsControlDown => leftControlDown || rightControlDown;

    private bool IsAltDown => leftAltDown || rightAltDown;
}
