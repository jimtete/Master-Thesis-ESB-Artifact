using System.Diagnostics;
using System.Text;
using System.Reflection;

namespace OlympusServiceBus.WebHost.Services;

public sealed class WebHostRestartService(
    IHostApplicationLifetime applicationLifetime,
    ILogger<WebHostRestartService> logger)
{
    private static readonly TimeSpan ShutdownDelay = TimeSpan.FromMilliseconds(250);
    private const string ProjectFileName = "OlympusServiceBus.WebHost.csproj";
    private const string RestartScriptRelativePath = "scripts\\Restart-WebHost.ps1";

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
        var developmentRestartCommand = TryBuildDevelopmentRestartCommand();
        if (developmentRestartCommand is not null)
        {
            return developmentRestartCommand;
        }

        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Process path is unavailable. The WebHost cannot restart itself.");
        }

        var executableName = Path.GetFileNameWithoutExtension(processPath);
        var commandLineArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();

        if (string.Equals(executableName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath) || !File.Exists(entryAssemblyPath))
            {
                throw new InvalidOperationException(
                    "The WebHost is running under dotnet, but the entry assembly path could not be resolved for restart.");
            }

            return new RestartCommand(processPath, [entryAssemblyPath, .. commandLineArguments]);
        }

        return new RestartCommand(processPath, commandLineArguments);
    }

    private static RestartCommand? TryBuildDevelopmentRestartCommand()
    {
        var projectPath = TryFindProjectFilePath();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var repoRoot = Directory.GetParent(projectDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return null;
        }

        var restartScriptPath = Path.Combine(repoRoot, RestartScriptRelativePath);
        if (!File.Exists(restartScriptPath))
        {
            return null;
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(entryAssemblyPath) || !File.Exists(entryAssemblyPath))
        {
            return null;
        }

        var configuration = ResolveBuildConfiguration(entryAssemblyPath);
        return new RestartCommand(
            "powershell.exe",
            [
                "-NoProfile",
                "-WindowStyle", "Hidden",
                "-ExecutionPolicy", "Bypass",
                "-File", restartScriptPath,
                "-Configuration", configuration
            ],
            BypassWindowsWrapper: true);
    }

    private static string? TryFindProjectFilePath()
    {
        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(entryAssemblyPath) || !File.Exists(entryAssemblyPath))
        {
            return null;
        }

        var entryDirectory = Path.GetDirectoryName(entryAssemblyPath);
        if (string.IsNullOrWhiteSpace(entryDirectory))
        {
            return null;
        }

        var searchDirectory = new DirectoryInfo(entryDirectory);
        while (searchDirectory is not null)
        {
            var candidateProjectPath = Path.Combine(searchDirectory.FullName, ProjectFileName);
            if (File.Exists(candidateProjectPath))
            {
                return candidateProjectPath;
            }

            searchDirectory = searchDirectory.Parent;
        }

        return null;
    }

    private static string ResolveBuildConfiguration(string entryAssemblyPath)
    {
        var pathParts = entryAssemblyPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (pathParts.Any(static part => string.Equals(part, "Release", StringComparison.OrdinalIgnoreCase)))
        {
            return "Release";
        }

        return "Debug";
    }

    private static void StartRestartHelper(RestartCommand restartCommand)
    {
        if (OperatingSystem.IsWindows())
        {
            if (restartCommand.BypassWindowsWrapper)
            {
                var directStartInfo = new ProcessStartInfo
                {
                    FileName = restartCommand.FilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                foreach (var argument in restartCommand.Arguments)
                {
                    directStartInfo.ArgumentList.Add(argument);
                }

                Process.Start(directStartInfo);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildWindowsRestartArguments(restartCommand),
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
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

    private static string BuildWindowsRestartArguments(RestartCommand restartCommand)
    {
        var filePath = QuoteForPowerShellSingleQuoted(restartCommand.FilePath);
        var workingDirectory = QuoteForPowerShellSingleQuoted(Path.GetDirectoryName(restartCommand.FilePath) ?? AppContext.BaseDirectory);
        var arguments = restartCommand.Arguments.Length == 0
            ? "@()"
            : $"@({string.Join(", ", restartCommand.Arguments.Select(QuoteForPowerShellSingleQuoted))})";

        return string.Join(" ",
        [
            "-NoProfile",
            "-WindowStyle", "Hidden",
            "-Command",
            QuoteForWindowsCommand(
                $"Start-Sleep -Seconds 2; Start-Process -WindowStyle Hidden -WorkingDirectory {workingDirectory} -FilePath {filePath} -ArgumentList {arguments}")
        ]);
    }

    private static string QuoteForWindowsCommand(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string QuoteForPowerShellSingleQuoted(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string QuoteForShell(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }

    private sealed record RestartCommand(string FilePath, string[] Arguments, bool BypassWindowsWrapper = false);
}
