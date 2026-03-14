namespace WinSwitch;

internal static class UpdateConfiguration
{
    public const string GitHubOwner = "YOUR_GITHUB_USERNAME";
    public const string GitHubRepo = "WinSwitch";

    public static bool IsConfigured =>
        !string.Equals(GitHubOwner, "YOUR_GITHUB_USERNAME", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(GitHubOwner) &&
        !string.IsNullOrWhiteSpace(GitHubRepo);
}
