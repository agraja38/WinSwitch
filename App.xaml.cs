using System.Windows;

namespace WinSwitch;

public partial class App : System.Windows.Application
{
    private SwitcherController? controller;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(args.Exception.Message, "WinSwitch error");
            args.Handled = true;
        };

        controller = new SwitcherController();
        controller.Start();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        controller?.Dispose();
        base.OnExit(e);
    }
}
