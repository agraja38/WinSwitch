using System.Windows;

namespace WinSwitch;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public void ShowProgress(string status, double percent)
    {
        StatusTextBlock.Text = status;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = Math.Clamp(percent, 0, 100);
    }

    public void ShowIndeterminate(string status)
    {
        StatusTextBlock.Text = status;
        ProgressBar.IsIndeterminate = true;
    }
}
