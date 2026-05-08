using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.BackgroundRuntimeService;

public sealed class BackgroundRuntimeService : IBackgroundRuntimeService
{
    private const string WebHostAddress = "127.0.0.1";
    private const int WebHostPort = 5099;
    private const string SwaggerPath = "swagger/index.html";
    private const string StartScriptRelativePath = @"Scripts\Start-DemoRuntime.ps1";

    public string SwaggerUiUrl => $"http://localhost:{WebHostPort}/{SwaggerPath}";

    public async Task<BackgroundRuntimeResult> EnsureStartedAsync()
    {
        var startScriptPath = ResolveStartScriptPath();
        if (string.IsNullOrWhiteSpace(startScriptPath))
        {
            return new BackgroundRuntimeResult(
                Success: false,
                Message: "Background runtime auto-start is available from the installed application package.",
                IsUnsupported: true);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{startScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var standardOutput = (await standardOutputTask).Trim();
            var standardError = (await standardErrorTask).Trim();

            if (process.ExitCode != 0)
            {
                var errorMessage = !string.IsNullOrWhiteSpace(standardError)
                    ? standardError
                    : $"Background runtime startup failed with exit code {process.ExitCode}.";

                return new BackgroundRuntimeResult(false, errorMessage);
            }

            if (!await IsWebHostReachableAsync())
            {
                return new BackgroundRuntimeResult(
                    false,
                    $"The WebHost did not become reachable at {SwaggerUiUrl}.");
            }

            var message = standardOutput.Contains("already running", StringComparison.OrdinalIgnoreCase)
                ? "Background runtime is already running. The workers and WebHost will stay active after you close the configurator."
                : "Background runtime is running. The workers and WebHost will stay active after you close the configurator.";

            return new BackgroundRuntimeResult(true, message);
        }
        catch (Exception ex)
        {
            return new BackgroundRuntimeResult(false, $"Failed to start the background runtime: {ex.Message}");
        }
    }

    public async Task<BackgroundRuntimeResult> OpenSwaggerUiAsync()
    {
        var startResult = await EnsureStartedAsync();
        if (!startResult.Success)
        {
            return startResult;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SwaggerUiUrl,
                UseShellExecute = true
            });

            return new BackgroundRuntimeResult(
                true,
                $"Opened the Web API Swagger UI at {SwaggerUiUrl}.");
        }
        catch (Exception ex)
        {
            return new BackgroundRuntimeResult(
                false,
                $"Background runtime is running, but the browser could not be opened automatically: {ex.Message}");
        }
    }

    private static string? ResolveStartScriptPath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var applicationDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(applicationDirectory))
        {
            return null;
        }

        var installRoot = Directory.GetParent(applicationDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            return null;
        }

        var startScriptPath = Path.Combine(installRoot, StartScriptRelativePath);
        return File.Exists(startScriptPath) ? startScriptPath : null;
    }

    private static async Task<bool> IsWebHostReachableAsync()
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(WebHostAddress, WebHostPort, timeoutTokenSource.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
