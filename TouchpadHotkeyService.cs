using System.Windows.Interop;

namespace WinSwitch;

public sealed class TouchpadHotkeyService : IDisposable
{
    private const int PreviousHotkeyId = 0x1201;
    private const int NextHotkeyId = 0x1202;

    private readonly HwndSource hwndSource;
    private bool registered;
    private bool enabled = true;

    public event Action<int>? StepRequested;

    public bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    public TouchpadHotkeyService()
    {
        var parameters = new HwndSourceParameters("WinSwitchTouchpadHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new nint(-3),
        };

        hwndSource = new HwndSource(parameters);
        hwndSource.AddHook(WndProc);
    }

    public void Start()
    {
        if (registered)
        {
            return;
        }

        var previousRegistered = NativeMethods.RegisterHotKey(
            hwndSource.Handle,
            PreviousHotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
            NativeMethods.VK_LEFT);
        var nextRegistered = NativeMethods.RegisterHotKey(
            hwndSource.Handle,
            NextHotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
            NativeMethods.VK_RIGHT);

        if (!previousRegistered || !nextRegistered)
        {
            if (previousRegistered)
            {
                NativeMethods.UnregisterHotKey(hwndSource.Handle, PreviousHotkeyId);
            }

            if (nextRegistered)
            {
                NativeMethods.UnregisterHotKey(hwndSource.Handle, NextHotkeyId);
            }

            throw new InvalidOperationException("Unable to register the touchpad hotkeys.");
        }

        registered = true;
    }

    public void Dispose()
    {
        if (registered)
        {
            NativeMethods.UnregisterHotKey(hwndSource.Handle, PreviousHotkeyId);
            NativeMethods.UnregisterHotKey(hwndSource.Handle, NextHotkeyId);
            registered = false;
        }

        hwndSource.RemoveHook(WndProc);
        hwndSource.Dispose();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY)
        {
            return 0;
        }

        var id = wParam.ToInt32();
        if (!Enabled)
        {
            return 0;
        }

        if (id == PreviousHotkeyId)
        {
            StepRequested?.Invoke(-1);
            handled = true;
        }
        else if (id == NextHotkeyId)
        {
            StepRequested?.Invoke(1);
            handled = true;
        }

        return 0;
    }
}
