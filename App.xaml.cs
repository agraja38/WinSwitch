using System.Windows;

namespace WinSwitch;

public partial class App : System.Windows.Application
{
    private SwitcherController? controller;
    private SingleInstanceManager? singleInstanceManager;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(args.Exception.Message, "WinSwitch error");
            args.Handled = true;
        };

        singleInstanceManager = new SingleInstanceManager();
        if (!singleInstanceManager.IsPrimaryInstance)
        {
            SingleInstanceManager.SignalExistingInstance();
            Shutdown();
            return;
        }

        controller = new SwitcherController();
        controller.Start();
        singleInstanceManager.ShowSettingsRequested += OnShowSettingsRequested;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (singleInstanceManager is not null)
        {
            singleInstanceManager.ShowSettingsRequested -= OnShowSettingsRequested;
            singleInstanceManager.Dispose();
        }

        controller?.Dispose();
        base.OnExit(e);
    }

    private void OnShowSettingsRequested()
    {
        Dispatcher.Invoke(() => controller?.ShowSettings());
    }
}
