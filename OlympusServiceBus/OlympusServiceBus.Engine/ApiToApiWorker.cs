using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Models.Configuration;
using System.Text.Json.Serialization;


namespace OlympusServiceBus.Engine;

public class ApiToApiWorker(ILogger<ApiToApiWorker> logger, IHttpClientFactory httpClientFactory) : BackgroundService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromSeconds(10);
    private const string ContractsFolderName = "Configuration";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient();
        
        var contractsDir = ResolveContractsDirectory(ContractsFolderName);

        if (!Directory.Exists(contractsDir))
        {
            logger.LogWarning("Contracts directory not found: {Dir}. No contracts will be executed.", contractsDir);
            return;
        }
        
        logger.LogInformation("ApiToApiWorker started. Reading contracts from: {Dir}", contractsDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var contractFiles = Directory.EnumerateFiles(contractsDir, "*.json", SearchOption.TopDirectoryOnly)
                    .ToList();

                if (contractFiles.Count == 0)
                {
                    logger.LogDebug("No contract files found in {Dir}", contractsDir);
                }

                foreach (var contractFile in contractFiles)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var contract = TryLoadContract(contractFile);
                    if (contract == null)
                    {
                        continue;
                    }

                    await ExecuteContractOnce(client, contract, Path.GetFileName(contractFile), stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Worker loop failed.");
            }

            await Task.Delay(LoopInterval, stoppingToken);
        }
    }
    
    private ApiToApi? TryLoadContract(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var json = sr.ReadToEnd();

            var wrapper = JsonSerializer.Deserialize<ApiToApiDocument>(json, JsonOpts);
            if (wrapper?.ApiToApi is not null)
                return wrapper.ApiToApi;

            var direct = JsonSerializer.Deserialize<ApiToApi>(json, JsonOpts);
            if (direct is not null)
                return direct;

            logger.LogWarning("Could not deserialize contract file: {File}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse contract file: {File}", filePath);
            return null;
        }
    }



    private async Task ExecuteContractOnce(HttpClient client, ApiToApi contract, string contractName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contract.Source?.Endpoint) || string.IsNullOrWhiteSpace(contract.Sink?.Endpoint))
        {
            logger.LogWarning("[{Contract}] Missing Source.Endpoint or Sink.Endpoint.", contractName);
            return;        
        }

        contract.Source.Method ??= "GET";
        contract.Sink.Method ??= "POST";
        contract.Mappings ??= Array.Empty<ApiFieldConfig>();

        if (!contract.Source.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[{Contract}] PoC supports Source.Method=GET only. Found: {Method}",
                contractName, contract.Source.Method);
            return;
        }
        
        JsonObject? sourceObj;
        try
        {
            using var sourceResp = await client.GetAsync(contract.Source.Endpoint, ct);
            sourceResp.EnsureSuccessStatusCode();

            var sourceText = await sourceResp.Content.ReadAsStringAsync(ct);
            sourceObj = JsonNode.Parse(sourceText) as JsonObject;

            if (sourceObj is null)
            {
                logger.LogWarning("[{Contract}] Source returned non-object JSON.", contractName);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Contract}] Source call failed: {Endpoint}", contractName, contract.Source.Endpoint);
            return;
        }

        // 2) Map fields (rename only, same types assumed)
        var sinkPayload = new JsonObject();

        foreach (var m in contract.Mappings)
        {
            switch (m.TransformationType)
            {
                case TransformationType.Direct:
                    if (string.IsNullOrWhiteSpace(m.SourceFieldName) || string.IsNullOrWhiteSpace(m.SinkFieldName))
                    {
                        continue;
                    }

                    if (!TryGetValueCaseInsensitive(sourceObj, m.SourceFieldName, out var directValue) || directValue is null)
                    {
                        logger.LogDebug("[{Contract}] Source field missing: {Field}", contractName, m.SourceFieldName);
                        continue;
                    }
                    sinkPayload[m.SinkFieldName] = directValue.DeepClone();
                    break;
                case TransformationType.Split:
                    if (string.IsNullOrWhiteSpace(m.SourceFieldName) ||
                        m.SinkFields.Length == 0 ||
                        m.SinkFields.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }
                    
                    if (!TryGetValueCaseInsensitive(sourceObj, m.SourceFieldName, out var splitValue) || splitValue is null)
                    {
                        logger.LogDebug("[{Contract}] Source field missing: {Field}", contractName, m.SourceFieldName);
                        continue;
                    }

                    var input = splitValue.ToString();
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        logger.LogDebug("[{Contract}] Split source field empty: {Field}", contractName, m.SourceFieldName);
                        break;
                    }

                    var inputValues = m.Separator == " "
                        ? input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        : input.Split(m.Separator, StringSplitOptions.None).Select(p => p.Trim()).ToArray();

                    for (var i = 0; i < m.SinkFields.Length; i++)
                    {
                        var sinkField = m.SinkFields[i];
                        if (string.IsNullOrWhiteSpace(sinkField))
                        {
                            logger.LogDebug("[{Contract}] Split produced fewer parts than sink fields. Field {Field} gets nothing.",
                                contractName, sinkField);
                            break;
                        }
                        
                        sinkPayload[sinkField] = inputValues[i];
                    }

                    break;
                case TransformationType.Join:
                    if (string.IsNullOrWhiteSpace(m.SinkFieldName) ||
                        m.SourceFields is null ||
                        m.SourceFields.Length == 0 ||
                        m.SourceFields.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    var parts = new List<string>();

                    foreach (var sourceField in m.SourceFields)
                    {
                        if (string.IsNullOrWhiteSpace(sourceField))
                        {
                            continue;
                        }

                        if (!TryGetValueCaseInsensitive(sourceObj, sourceField, out var node) || node is null)
                        {
                            logger.LogDebug("[{Contract}] Join source field missing: {Field}", contractName, sourceField);
                            continue;
                        }

                        var value = node.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parts.Add(value.Trim());
                        }
                    }

                    if (parts.Count == 0)
                    {
                        logger.LogDebug("[{Contract}] Join produced no values for sink field: {Field}", contractName, m.SinkFieldName);
                        break;
                    }
                    
                    sinkPayload[m.SinkFieldName] = string.Join(m.Separator, parts);
                    break;
            };
        }

        if (sinkPayload.Count == 0)
        {
            logger.LogWarning("[{Contract}] No fields mapped. Skipping sink call.", contractName);
            return;
        }
        
        // 3) Push to sink
        try
        {
            var sinkMethod = new HttpMethod(contract.Sink.Method);

            using var req = new HttpRequestMessage(sinkMethod, contract.Sink.Endpoint)
            {
                Content = JsonContent.Create(sinkPayload)
            };

            using var sinkResp = await client.SendAsync(req, ct);
            sinkResp.EnsureSuccessStatusCode();

            logger.LogInformation("[{Contract}] Forwarded payload to sink. Payload: {Payload}",
                contractName,
                sinkPayload.ToJsonString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Contract}] Sink call failed: {Endpoint}", contractName, contract.Sink.Endpoint);
        }
    }

    private static bool TryGetValueCaseInsensitive(JsonObject obj, string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(key, out value))
        {
            return true;
        }

        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        
        value = null;
        return false;
    }

    private static string ResolveContractsDirectory(string folderName)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), folderName),
            Path.Combine(AppContext.BaseDirectory, folderName),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[1];
    }

    private sealed class ApiToApiDocument
    {
        public ApiToApi? ApiToApi { get; set; }
    }
}