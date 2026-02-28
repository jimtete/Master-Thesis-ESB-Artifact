namespace OlympusServiceBus.Utils.Configuration.File;

public sealed class LoopCsvRule
{
    public char Delimiter { get; init; } = ',';
    public bool HasHeader { get; init; } = true;

    public string[] RequiredColumns { get; init; } = [];

    public CsvColumnBinding[] Bindings { get; init; } = [];
}

public sealed class CsvColumnBinding
{
    public string Column { get; init; } = "";

    public string Field { get; init; } = "";

    public bool Required { get; init; } = false;

    public string? DefaultValue { get; init; } = null;
}