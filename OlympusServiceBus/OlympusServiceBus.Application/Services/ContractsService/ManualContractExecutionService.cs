using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Execution.FileToFile;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.Scheduling;
using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.Services.ContractsService;

public sealed class ManualContractExecutionService : IManualContractExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new SourceFieldJsonConverter(),
            new SinkFieldJsonConverter()
        }
    };

    private readonly IServiceProvider _serviceProvider;

    public ManualContractExecutionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<ManualContractExecutionResult> ExecuteAsync(
        string contractFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractFilePath))
        {
            return new ManualContractExecutionResult(false, "No contract file was selected.");
        }

        if (!File.Exists(contractFilePath))
        {
            return new ManualContractExecutionResult(false, $"Contract file was not found: {contractFilePath}");
        }

        var contract = await LoadContractAsync(contractFilePath, cancellationToken);

        if (contract is null)
        {
            return new ManualContractExecutionResult(false, "The selected file is not a supported manual contract.");
        }

        if (!contract.Enabled)
        {
            return new ManualContractExecutionResult(false, $"Contract '{contract.Name}' is disabled.");
        }

        if (contract.Schedule?.Mode != ContractScheduleMode.Manual)
        {
            return new ManualContractExecutionResult(false, $"Contract '{contract.Name}' is not configured for manual execution.");
        }

        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        switch (contract)
        {
            case ApiToApiContract apiToApi:
                await services.GetRequiredService<IApiToApiExecutionService>()
                    .ExecuteAsync(apiToApi, cancellationToken);
                break;

            case ApiToFileContract apiToFile:
                await services.GetRequiredService<IApiToFileExecutionService>()
                    .ExecuteAsync(apiToFile, cancellationToken);
                break;

            case FileToApiContract fileToApi:
                await services.GetRequiredService<FileToApiExecutor>()
                    .ExecuteOnce(fileToApi, cancellationToken);
                break;

            case FileToFileContract fileToFile:
                await services.GetRequiredService<FileToFileExecutor>()
                    .ExecuteOnce(fileToFile, cancellationToken);
                break;

            default:
                return new ManualContractExecutionResult(false, $"Manual execution is not supported for '{contract.GetType().Name}'.");
        }

        return new ManualContractExecutionResult(true, $"Contract '{contract.Name}' executed manually.");
    }

    private static async Task<ContractBase?> LoadContractAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var apiToApi = JsonSerializer.Deserialize<ApiToApiDocument>(json, JsonOptions)?.ApiToApi;
        if (apiToApi is not null)
        {
            return apiToApi;
        }

        var apiToFile = JsonSerializer.Deserialize<ApiToFileDocument>(json, JsonOptions)?.ApiToFile;
        if (apiToFile is not null)
        {
            return apiToFile;
        }

        var fileToApi = JsonSerializer.Deserialize<FileToApiDocument>(json, JsonOptions)?.FileToApi;
        if (fileToApi is not null)
        {
            return fileToApi;
        }

        return JsonSerializer.Deserialize<FileToFileDocument>(json, JsonOptions)?.FileToFile;
    }

    private sealed class ApiToApiDocument
    {
        public ApiToApiContract? ApiToApi { get; set; }
    }

    private sealed class ApiToFileDocument
    {
        public ApiToFileContract? ApiToFile { get; set; }
    }

    private sealed class FileToApiDocument
    {
        public FileToApiContract? FileToApi { get; set; }
    }

    private sealed class FileToFileDocument
    {
        public FileToFileContract? FileToFile { get; set; }
    }
}
