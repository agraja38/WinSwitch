using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinSwitch;

public sealed class GitHubReleaseUpdater
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task CheckForUpdatesAsync(
        bool showNoUpdateMessage,
        Action<string, string> onStatusMessage,
        Action<string> onError,
        Action<string, double?, bool>? onProgress = null,
        Func<Task>? beforeInstall = null)
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
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            onStatusMessage("Downloading update", $"Downloading {asset.Name} from GitHub Releases.");
            onProgress?.Invoke("Downloading update...", 0, false);

            var downloadPath = Path.Combine(Path.GetTempPath(), asset.Name);
            using var response = await HttpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var destination = File.Create(downloadPath))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        var percent = totalRead * 100d / totalBytes.Value;
                        onProgress?.Invoke($"Downloading update... {percent:0}%", percent, false);
                    }
                    else
                    {
                        onProgress?.Invoke("Downloading update...", null, true);
                    }
                }
            }

            if (beforeInstall is not null)
            {
                await beforeInstall();
            }

            onProgress?.Invoke("Installing update...", null, true);
            var launcherPath = CreateUpdateLauncher(downloadPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -STA -File \"{launcherPath}\"",
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
        var launcherPath = Path.Combine(Path.GetTempPath(), $"WinSwitch-Update-{Guid.NewGuid():N}.ps1");
        var currentProcessId = Environment.ProcessId;
        var installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "WinSwitch",
            "WinSwitch.exe");

        var script = string.Join(
            Environment.NewLine,
            "Add-Type -AssemblyName System.Windows.Forms",
            "Add-Type -AssemblyName System.Drawing",
            "$form = New-Object System.Windows.Forms.Form",
            "$form.Text = 'WinSwitch Update'",
            "$form.Width = 420",
            "$form.Height = 150",
            "$form.StartPosition = 'CenterScreen'",
            "$form.TopMost = $true",
            "$form.FormBorderStyle = 'FixedDialog'",
            "$form.MaximizeBox = $false",
            "$form.MinimizeBox = $false",
            "$label = New-Object System.Windows.Forms.Label",
            "$label.Text = 'Installing WinSwitch update...'",
            "$label.AutoSize = $false",
            "$label.Width = 360",
            "$label.Height = 24",
            "$label.Left = 20",
            "$label.Top = 20",
            "$progress = New-Object System.Windows.Forms.ProgressBar",
            "$progress.Style = 'Marquee'",
            "$progress.MarqueeAnimationSpeed = 30",
            "$progress.Width = 360",
            "$progress.Height = 20",
            "$progress.Left = 20",
            "$progress.Top = 60",
            "$form.Controls.Add($label)",
            "$form.Controls.Add($progress)",
            "$worker = New-Object System.ComponentModel.BackgroundWorker",
            "$worker.DoWork += {",
            $"  while (Get-Process -Id {currentProcessId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Seconds 1 }}",
            $"  $process = Start-Process -FilePath '{installerPath}' -ArgumentList '/VERYSILENT /NORESTART' -PassThru -Wait",
            "  $_.Result = $process.ExitCode",
            "}",
            "$worker.RunWorkerCompleted += {",
            "  $form.Close()",
            "  if ($_.Result -eq 0) {",
            $"    if (Test-Path '{installPath}') {{ Start-Process -FilePath '{installPath}' }}",
            "    [System.Windows.Forms.MessageBox]::Show('WinSwitch update complete.', 'WinSwitch', 'OK', 'Information') | Out-Null",
            "  } else {",
            "    [System.Windows.Forms.MessageBox]::Show('WinSwitch update failed.', 'WinSwitch', 'OK', 'Error') | Out-Null",
            "  }",
            $"  Remove-Item -LiteralPath '{launcherPath}' -Force -ErrorAction SilentlyContinue",
            "}",
            "$form.Add_Shown({ $worker.RunWorkerAsync() })",
            "[void]$form.ShowDialog()");

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
