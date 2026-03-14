using System.Runtime.InteropServices;

namespace WinSwitch;

public sealed class ForegroundHistoryService : IDisposable
{
    private readonly List<int> recentProcessIds = [];
    private readonly object syncRoot = new();
    private readonly Func<nint, bool> isSwitchableHandle;
    private readonly NativeMethods.WinEventDelegate callback;
    private nint eventHook;

    public ForegroundHistoryService(Func<nint, bool> isSwitchableHandle)
    {
        this.isSwitchableHandle = isSwitchableHandle;
        callback = HandleWinEvent;
    }

    public void Start()
    {
        if (eventHook != 0)
        {
            return;
        }

        eventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            0,
            callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow != 0 && isSwitchableHandle(foregroundWindow))
        {
            NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
            RecordProcess((int)processId);
        }
    }

    public IReadOnlyList<SwitchableApp> OrderApps(IReadOnlyList<SwitchableApp> apps)
    {
        var appMap = apps.ToDictionary(app => app.ProcessId);
        var ordered = new List<SwitchableApp>(apps.Count);

        lock (syncRoot)
        {
            foreach (var processId in recentProcessIds)
            {
                if (appMap.Remove(processId, out var app))
                {
                    ordered.Add(app);
                }
            }
        }

        ordered.AddRange(appMap.Values.OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    public void RecordProcess(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        lock (syncRoot)
        {
            recentProcessIds.Remove(processId);
            recentProcessIds.Insert(0, processId);

            if (recentProcessIds.Count > 64)
            {
                recentProcessIds.RemoveRange(64, recentProcessIds.Count - 64);
            }
        }
    }

    public void Dispose()
    {
        if (eventHook != 0)
        {
            NativeMethods.UnhookWinEvent(eventHook);
            eventHook = 0;
        }

        GC.KeepAlive(callback);
    }

    private void HandleWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND || hwnd == 0)
        {
            return;
        }

        if (!isSwitchableHandle(hwnd))
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        RecordProcess((int)processId);
    }
}
