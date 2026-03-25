using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Validation;

public sealed class PortToApiInboundValidator
{
    public List<string> Validate(JsonObject inbound, PortToApiContract c)
    {
        var errors = new List<string>();

        // Prefer explicit Request.Fields if present
        var fields = c.Request?.Fields ?? Array.Empty<PortToApiRequestField>();

        if (fields.Length == 0)
        {
            // Fallback: infer required fields from mappings (PoC behavior)
            var inferred = InferInboundFieldsFromMappings(c);
            foreach (var name in inferred)
            {
                var exists = inbound.Any(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                    errors.Add($"Missing required field: {name}");
            }

            return errors;
        }

        foreach (var f in fields)
        {
            var name = f.FieldName;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var exists = inbound.Any(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
            if (!exists && f.Required)
                errors.Add($"Missing required field: {name}");
        }

        return errors;
    }

    private static IEnumerable<string> InferInboundFieldsFromMappings(PortToApiContract c)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var mappings = c.Mappings ?? Array.Empty<ApiFieldConfig>();

        foreach (var m in mappings)
        {
            switch (m.TransformationType)
            {
                case TransformationType.Direct:
                case TransformationType.Split:
                    if (!m.SourceFieldName.IsEmpty && m.SourceFieldName.Value is not null)
                        set.Add(m.SourceFieldName.Value);
                    break;

                case TransformationType.Join:
                    if (m.SourceFields is { Length: > 0 })
                    {
                        foreach (var sf in m.SourceFields)
                        {
                            if (!sf.IsEmpty && sf.Value is not null)
                                set.Add(sf.Value);
                        }
                    }
                    break;
            }
        }

        return set;
    }
}