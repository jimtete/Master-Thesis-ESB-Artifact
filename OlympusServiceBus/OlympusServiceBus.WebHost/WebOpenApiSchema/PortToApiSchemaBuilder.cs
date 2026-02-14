using Microsoft.OpenApi;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.WebOpenApiSchema;

public sealed class PortToApiSchemaBuilder
{
    public OpenApiSchema BuildFromContract(PortToApiContract c)
    {
        var fields = c.Request?.Fields ?? Array.Empty<PortToApiRequestField>();

        var props = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fields)
        {
            var name = f.FieldName;
            if (string.IsNullOrWhiteSpace(name)) continue;

            props[name] = new OpenApiSchema
            {
                Type = f.Type switch
                {
                    JsonFieldType.String  => JsonSchemaType.String,
                    JsonFieldType.Integer => JsonSchemaType.Integer,
                    JsonFieldType.Number  => JsonSchemaType.Number,
                    JsonFieldType.Boolean => JsonSchemaType.Boolean,
                    JsonFieldType.Object  => JsonSchemaType.Object,
                    JsonFieldType.Array   => JsonSchemaType.Array,
                    _                     => JsonSchemaType.String
                },
                Format = string.IsNullOrWhiteSpace(f.Format) ? null : f.Format
            };

            if (f.Required) required.Add(name);
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = props,
            Required = required
        };
    }

    public OpenApiSchema BuildFromFieldNames(IEnumerable<string> names)
    {
        var props = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            props[name] = new OpenApiSchema { Type = JsonSchemaType.String };
            required.Add(name);
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = props,
            Required = required
        };
    }
}
