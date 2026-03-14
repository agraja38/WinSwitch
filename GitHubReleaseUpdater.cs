using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace WinSwitch;

public sealed class GitHubReleaseUpdater
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task CheckForUpdatesAsync(bool showNoUpdateMessage, Action<string, string> onStatusMessage, Action<string> onError, Func<Task>? beforeInstall = null)
    {
        if (!UpdateConfiguration.IsConfigured)
        {
            if (showNoUpdateMessage)
            {
                onStatusMessage("Updates not configured", "Set your GitHub owner and repo in UpdateConfiguration.cs before publishing releases.");
            }

            return;
        }

        try
        {
            var release = await GetLatestReleaseAsync();
            if (release is null)
            {
                if (showNoUpdateMessage)
                {
                    onStatusMessage("No release found", "No published GitHub release is available yet.");
                }

                return;
            }

            var currentVersion = GetCurrentVersion();
            if (!Version.TryParse(release.TagName.TrimStart('v', 'V'), out var latestVersion))
            {
                onError($"The latest release tag '{release.TagName}' is not a valid version.");
                return;
            }

            if (latestVersion <= currentVersion)
            {
                if (showNoUpdateMessage)
                {
                    onStatusMessage("WinSwitch is up to date", $"You already have version {currentVersion}.");
                }

                return;
            }

            var assetName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "WinSwitch-Setup-ARM64.exe"
                : "WinSwitch-Setup-x64.exe";
            var asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                onError($"Release {release.TagName} does not contain the expected installer asset '{assetName}'.");
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"WinSwitch {latestVersion} is available. Download and install it now?",
                "Update available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            onStatusMessage("Downloading update", $"Downloading {asset.Name} from GitHub Releases.");
            var downloadPath = Path.Combine(Path.GetTempPath(), asset.Name);

            await using (var source = await HttpClient.GetStreamAsync(asset.BrowserDownloadUrl))
            await using (var destination = File.Create(downloadPath))
            {
                await source.CopyToAsync(destination);
            }

            if (beforeInstall is not null)
            {
                await beforeInstall();
            }

            var launcherPath = CreateUpdateLauncher(downloadPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = launcherPath,
                UseShellExecute = true,
            });

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            onError(ex.Message);
        }
    }

    private static async Task<GitHubReleaseResponse?> GetLatestReleaseAsync()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{UpdateConfiguration.GitHubOwner}/{UpdateConfiguration.GitHubRepo}/releases/latest");
        using var response = await HttpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream);
    }

    private static Version GetCurrentVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?.Split('+', 2)[0];

        return Version.TryParse(informationalVersion, out var version)
            ? version
            : Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WinSwitch", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static string CreateUpdateLauncher(string installerPath)
    {
        var launcherPath = Path.Combine(Path.GetTempPath(), $"WinSwitch-Update-{Guid.NewGuid():N}.cmd");
        var currentProcessId = Environment.ProcessId;
        var installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "WinSwitch",
            "WinSwitch.exe");

        var script = string.Join(
            Environment.NewLine,
            "@echo off",
            $"set PID={currentProcessId}",
            ":waitloop",
            "tasklist /FI \"PID eq %PID%\" | find \"%PID%\" >nul",
            "if not errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto waitloop",
            ")",
            $"start \"\" /wait \"{installerPath}\" /VERYSILENT /NORESTART",
            $"if exist \"{installPath}\" start \"\" \"{installPath}\"",
            "del \"%~f0\"");

        File.WriteAllText(launcherPath, script);
        return launcherPath;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; init; }

        [JsonPropertyName("assets")]
        public required List<GitHubReleaseAsset> Assets { get; init; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public required string BrowserDownloadUrl { get; init; }
    }
}
