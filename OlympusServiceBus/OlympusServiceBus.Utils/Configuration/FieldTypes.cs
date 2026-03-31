using System.Text.Json;
using System.Text.Json.Serialization;

namespace OlympusServiceBus.Utils.Configuration;

[JsonConverter(typeof(SourceFieldJsonConverter))]
public readonly record struct SourceField(string? Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    
    public override string ToString() => Value ?? string.Empty;

    public static implicit operator SourceField(string? value) => new(value);
    public static implicit operator string?(SourceField field) => field.Value;
}

public sealed class SourceFieldJsonConverter : JsonConverter<SourceField>
{
    public override SourceField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new SourceField(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return new SourceField(null);
        }
        
        throw new JsonException("Expected SourceField as a JSON string.");
    }

    public override void Write(Utf8JsonWriter writer, SourceField value, JsonSerializerOptions options)
    {
        if (value.Value is null)
        {
            writer.WriteNullValue();
            return;
        }
        
        writer.WriteStringValue(value.Value);
    }
}

[JsonConverter(typeof(SinkFieldJsonConverter))]
public readonly record struct SinkField(string? Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value ?? string.Empty;

    public static implicit operator SinkField(string? value) => new(value);
    public static implicit operator string?(SinkField field) => field.Value;
}

public sealed class SinkFieldJsonConverter : JsonConverter<SinkField>
{
    public override SinkField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new SinkField(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return new SinkField(null);
        }
        
        throw new JsonException("Expected SinkField as a JSON string.");
    }

    public override void Write(Utf8JsonWriter writer, SinkField value, JsonSerializerOptions options)
    {
        if (value.Value is null)
        {
            writer.WriteNullValue();
            return;
        }
        
        writer.WriteStringValue(value.Value);
    }
}