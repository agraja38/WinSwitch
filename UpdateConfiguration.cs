namespace WinSwitch;

internal static class UpdateConfiguration
{
    public const string GitHubOwner = "agraja38";
    public const string GitHubRepo = "WinSwitch";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GitHubOwner) &&
        !string.IsNullOrWhiteSpace(GitHubRepo);
}
