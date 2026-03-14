using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.ApiToApi;

public class ApiToApiExecutor
{
    private readonly ILogger<ApiToApiExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiToApiExecutor(ILogger<ApiToApiExecutor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiToApiExecutionResult?> BuildExecutionAsync(
        ApiToApiContract contract,
        CancellationToken cancellationToken)
    {
        if (!contract.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(contract.Source?.Endpoint) || string.IsNullOrWhiteSpace(contract.Sink?.Endpoint))
        {
            _logger.LogWarning("[{Contract}] Missing Source.Endpoint or Sink.Endpoint.", contract.ContractId);
            return null;
        }

        contract.Source.Method ??= "GET";
        contract.Sink.Method ??= "POST";

        if (!contract.Source.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[{Contract}] PoC supports Source.Method=GET only. Found: {Method}",
                contract.ContractId,
                contract.Source.Method);

            return null;
        }

        var client = _httpClientFactory.CreateClient();

        JsonObject? sourceObj;
        try
        {
            using var resp = await client.GetAsync(contract.Source.Endpoint, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var text = await resp.Content.ReadAsStringAsync(cancellationToken);
            sourceObj = JsonNode.Parse(text) as JsonObject;

            if (sourceObj is null)
            {
                _logger.LogWarning("[{Contract}] Source returned non-object JSON.", contract.ContractId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Contract}] Source call failed: {Endpoint}",
                contract.ContractId,
                contract.Source.Endpoint);

            throw;
        }

        var sinkPayload = new JsonObject();

        foreach (var m in contract.Mappings ?? Array.Empty<ApiFieldConfig>())
        {
            switch (m.TransformationType)
            {
                case TransformationType.Direct:
                {
                    if (string.IsNullOrWhiteSpace(m.SourceFieldName) || string.IsNullOrWhiteSpace(m.SinkFieldName))
                        break;

                    if (!TryGetValueCaseInsensitive(sourceObj, m.SourceFieldName, out var v) || v is null)
                        break;

                    sinkPayload[m.SinkFieldName] = v.DeepClone();
                    break;
                }

                case TransformationType.Split:
                {
                    if (string.IsNullOrWhiteSpace(m.SourceFieldName) ||
                        m.SinkFields is null || m.SinkFields.Length == 0 ||
                        m.SinkFields.All(string.IsNullOrWhiteSpace))
                        break;

                    if (!TryGetValueCaseInsensitive(sourceObj, m.SourceFieldName, out var node) || node is null)
                        break;

                    var input = node.ToString();
                    if (string.IsNullOrWhiteSpace(input))
                        break;

                    var sep = string.IsNullOrEmpty(m.Separator) ? " " : m.Separator;

                    var parts = sep == " "
                        ? input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        : input.Split(sep, StringSplitOptions.None).Select(p => p.Trim()).ToArray();

                    for (var i = 0; i < m.SinkFields.Length; i++)
                    {
                        var sinkField = m.SinkFields[i];
                        if (string.IsNullOrWhiteSpace(sinkField))
                            continue;

                        if (i >= parts.Length)
                            break;

                        sinkPayload[sinkField] = parts[i];
                    }

                    break;
                }

                case TransformationType.Join:
                {
                    if (string.IsNullOrWhiteSpace(m.SinkFieldName) ||
                        m.SourceFields is null || m.SourceFields.Length == 0 ||
                        m.SourceFields.All(string.IsNullOrWhiteSpace))
                        break;

                    var sep = string.IsNullOrEmpty(m.Separator) ? " " : m.Separator;

                    var values = new List<string>();

                    foreach (var f in m.SourceFields)
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        if (!TryGetValueCaseInsensitive(sourceObj, f, out var n) || n is null)
                            continue;

                        var s = n.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            values.Add(s.Trim());
                    }

                    if (values.Count == 0)
                        break;

                    sinkPayload[m.SinkFieldName] = string.Join(sep, values);
                    break;
                }
            }
        }

        if (sinkPayload.Count == 0)
        {
            _logger.LogWarning("[{Contract}] No fields mapped. Skipping sink call.", contract.ContractId);
            return null;
        }

        return new ApiToApiExecutionResult
        {
            SourcePayload = sourceObj,
            SinkPayload = sinkPayload
        };
    }

    public async Task<JsonObject?> SendPayloadAsync(
        ApiToApiContract contract,
        JsonObject sinkPayload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var req = new HttpRequestMessage(
                new HttpMethod(contract.Sink.Method ?? "POST"),
                contract.Sink.Endpoint)
            {
                Content = JsonContent.Create(sinkPayload)
            };

            using var resp = await client.SendAsync(req, cancellationToken);
            var responseText = await resp.Content.ReadAsStringAsync(cancellationToken);

            resp.EnsureSuccessStatusCode();

            JsonObject? responsePayload = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                responsePayload = JsonNode.Parse(responseText) as JsonObject;
            }

            _logger.LogInformation(
                "[{Contract}] Forwarded payload to sink. Payload: {Payload}",
                contract.ContractId,
                sinkPayload.ToJsonString());

            return responsePayload;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Contract}] Sink call failed: {Endpoint}",
                contract.ContractId,
                contract.Sink.Endpoint);

            throw;
        }
    }

    private static bool TryGetValueCaseInsensitive(JsonObject obj, string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(key, out value))
            return true;

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
}