using System.Windows.Media;

namespace WinSwitch;

public sealed class SwitchableApp
{
    public required int ProcessId { get; init; }
    public required string DisplayName { get; init; }
    public required string WindowTitle { get; init; }
    public required nint WindowHandle { get; init; }
    public required ImageSource? Icon { get; init; }
    public required string ExecutablePath { get; init; }
    public bool IsSelected { get; set; }
}
