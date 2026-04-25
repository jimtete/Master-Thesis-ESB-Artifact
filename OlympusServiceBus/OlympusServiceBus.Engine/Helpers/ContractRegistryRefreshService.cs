using System.Text.Json;
using System.Threading.Channels;
using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Execution.FeedbackContracts;
using OlympusServiceBus.Engine.Scheduling;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public sealed class ContractRegistryRefreshService(
    IServiceScopeFactory scopeFactory,
    IContractRegistry contractRegistry,
    PortToApiReloadClient reloadClient,
    ILogger<ContractRegistryRefreshService> logger) : BackgroundService, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly Channel<bool> _refreshRequests = Channel.CreateUnbounded<bool>();
    private FileSystemWatcher? _watcher;
    private string[] _lastPortContractFingerprints = [];

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var contractsDirectoryPath = GetContractsDirectoryPath();
        Directory.CreateDirectory(contractsDirectoryPath);

        _lastPortContractFingerprints = BuildPortContractFingerprints(contractRegistry.AllContracts);

        _watcher = new FileSystemWatcher(contractsDirectoryPath, "*.json")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.CreationTime
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size
        };

        _watcher.Created += OnContractsChanged;
        _watcher.Changed += OnContractsChanged;
        _watcher.Deleted += OnContractsChanged;
        _watcher.Renamed += OnContractsRenamed;
        _watcher.EnableRaisingEvents = true;

        logger.LogInformation("Watching contracts directory for runtime changes: {ContractsDirectory}", contractsDirectoryPath);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _refreshRequests.Reader;

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            while (reader.TryRead(out _))
            {
            }

            await Task.Delay(DebounceDelay, stoppingToken);

            while (reader.TryRead(out _))
            {
            }

            await ReloadContractsAsync(stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
        }

        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Created -= OnContractsChanged;
            _watcher.Changed -= OnContractsChanged;
            _watcher.Deleted -= OnContractsChanged;
            _watcher.Renamed -= OnContractsRenamed;
            _watcher.Dispose();
        }

        base.Dispose();
    }

    private void OnContractsChanged(object? sender, FileSystemEventArgs e)
    {
        if (!LooksLikeJsonContractFile(e.FullPath))
        {
            return;
        }

        logger.LogDebug("Detected contract file change: {ChangeType} {FilePath}", e.ChangeType, e.FullPath);
        _refreshRequests.Writer.TryWrite(true);
    }

    private void OnContractsRenamed(object? sender, RenamedEventArgs e)
    {
        if (!LooksLikeJsonContractFile(e.OldFullPath) &&
            !LooksLikeJsonContractFile(e.FullPath))
        {
            return;
        }

        logger.LogDebug(
            "Detected contract file rename: {OldFilePath} -> {NewFilePath}",
            e.OldFullPath,
            e.FullPath);

        _refreshRequests.Writer.TryWrite(true);
    }

    private async Task ReloadContractsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var loader = scope.ServiceProvider.GetRequiredService<IContractLoader>();
            var registry = scope.ServiceProvider.GetRequiredService<IContractRegistry>();
            var feedbackContractRegistry = scope.ServiceProvider.GetRequiredService<IFeedbackContractRegistry>();
            var contractsDirectoryPath = GetContractsDirectoryPath();

            var contracts = loader.LoadAllContracts(contractsDirectoryPath);
            ContractSchedulingBootstrapValidator.ValidateAll(contracts);

            var feedbackContracts = loader.LoadAllFeedbackContracts(contractsDirectoryPath);

            registry.SetAllContracts(contracts);
            feedbackContractRegistry.SetAllFeedbackContracts(feedbackContracts);

            logger.LogInformation(
                "Reloaded contract registries after file-system change. Forward={ForwardCount}, Feedback={FeedbackCount}",
                contracts.Count,
                feedbackContracts.Count);

            var currentPortContractFingerprints = BuildPortContractFingerprints(contracts);
            var shouldReloadWebHost = !_lastPortContractFingerprints.SequenceEqual(
                currentPortContractFingerprints,
                StringComparer.Ordinal);

            _lastPortContractFingerprints = currentPortContractFingerprints;

            if (shouldReloadWebHost)
            {
                logger.LogInformation("PortTo contract set changed. Triggering WebHost reload.");
                await reloadClient.ReloadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh contract registries after a file-system change.");
        }
    }

    private static string[] BuildPortContractFingerprints(IEnumerable<ContractBase> contracts)
    {
        return contracts
            .Where(static contract => contract is PortToApiContract or PortToFileContract)
            .Select(static contract => $"{contract.GetType().Name}:{JsonSerializer.Serialize(contract, contract.GetType())}")
            .OrderBy(static fingerprint => fingerprint, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool LooksLikeJsonContractFile(string? filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) &&
               string.Equals(Path.GetExtension(filePath), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContractsDirectoryPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "OlympusServiceBus", "Contracts");
    }
}
