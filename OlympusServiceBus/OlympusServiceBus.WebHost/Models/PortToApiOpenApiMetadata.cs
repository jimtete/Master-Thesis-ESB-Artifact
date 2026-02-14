using Microsoft.OpenApi;

namespace OlympusServiceBus.WebHost.Models;

public sealed record PortToApiOpenApiMetadata(string ContractId, OpenApiSchema RequestSchema);