using System.Threading;

namespace WinSwitch;

public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\WinSwitchPrimaryMutex";
    private const string ShowSettingsEventName = @"Local\WinSwitchShowSettingsEvent";

    private readonly Mutex mutex;
    private readonly EventWaitHandle showSettingsEvent;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread? listenerThread;

    public SingleInstanceManager()
    {
        mutex = new Mutex(true, MutexName, out var createdNew);
        showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        IsPrimaryInstance = createdNew;

        if (IsPrimaryInstance)
        {
            listenerThread = new Thread(ListenForSignals)
            {
                IsBackground = true,
                Name = "WinSwitchSingleInstanceListener",
            };
            listenerThread.Start();
        }
    }

    public bool IsPrimaryInstance { get; }

    public event Action? ShowSettingsRequested;

    public static void SignalExistingInstance()
    {
        try
        {
            using var existingEvent = EventWaitHandle.OpenExisting(ShowSettingsEventName);
            existingEvent.Set();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        showSettingsEvent.Set();

        if (IsPrimaryInstance)
        {
            mutex.ReleaseMutex();
        }

        showSettingsEvent.Dispose();
        mutex.Dispose();
        cancellationTokenSource.Dispose();
    }

    private void ListenForSignals()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            showSettingsEvent.WaitOne();
            if (cancellationTokenSource.IsCancellationRequested)
            {
                break;
            }

            ShowSettingsRequested?.Invoke();
        }
    }
}
