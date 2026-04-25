using System.Diagnostics;
using System.Text;

namespace OlympusServiceBus.WebHost.Services;

public sealed class WebHostRestartService(
    IHostApplicationLifetime applicationLifetime,
    ILogger<WebHostRestartService> logger)
{
    private static readonly TimeSpan ShutdownDelay = TimeSpan.FromMilliseconds(250);

    public bool TryScheduleRestart(out string? error)
    {
        try
        {
            var restartCommand = BuildRestartCommand();
            StartRestartHelper(restartCommand);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ShutdownDelay);
                }
                catch
                {
                }

                applicationLifetime.StopApplication();
            });

            logger.LogInformation("WebHost restart scheduled.");
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule WebHost restart.");
            error = ex.Message;
            return false;
        }
    }

    private static RestartCommand BuildRestartCommand()
    {
        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Process path is unavailable. The WebHost cannot restart itself.");
        }

        return new RestartCommand(processPath, Environment.GetCommandLineArgs().Skip(1).ToArray());
    }

    private static void StartRestartHelper(RestartCommand restartCommand)
    {
        if (OperatingSystem.IsWindows())
        {
            var startCommand = new StringBuilder("ping 127.0.0.1 -n 3 > nul && start \"\" ");
            startCommand.Append(QuoteForWindowsCommand(restartCommand.FilePath));

            if (restartCommand.Arguments.Length > 0)
            {
                startCommand.Append(' ');
                startCommand.Append(string.Join(" ", restartCommand.Arguments.Select(QuoteForWindowsCommand)));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {startCommand}",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            return;
        }

        var shellCommand = new StringBuilder("sleep 2 && exec ");
        shellCommand.Append(QuoteForShell(restartCommand.FilePath));

        if (restartCommand.Arguments.Length > 0)
        {
            shellCommand.Append(' ');
            shellCommand.Append(string.Join(" ", restartCommand.Arguments.Select(QuoteForShell)));
        }

        shellCommand.Append(" >/dev/null 2>&1 &");

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c {QuoteForShell(shellCommand.ToString())}",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static string QuoteForWindowsCommand(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string QuoteForShell(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }

    private sealed record RestartCommand(string FilePath, string[] Arguments);
}
