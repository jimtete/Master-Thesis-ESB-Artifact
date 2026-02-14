using Microsoft.OpenApi;
using OlympusServiceBus.WebHost.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OlympusServiceBus.WebHost.OpenApi;

public sealed class PortToApiOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var meta = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<PortToApiOpenApiMetadata>()
            .FirstOrDefault();

        if (meta is null)
            return;

        operation.Summary = $"PortToApi: {meta.ContractId}";

        // IMPORTANT: OpenAPI.NET v2 uses tag references and they are immutable-ish
        operation.Tags = new HashSet<OpenApiTagReference>
        {
            new OpenApiTagReference("PortToApi")
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = meta.RequestSchema
                }
            }
        };
    }
}