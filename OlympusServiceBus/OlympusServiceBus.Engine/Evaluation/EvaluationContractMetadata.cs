using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Evaluation;

public sealed record EvaluationContractMetadata(
    string ContractType,
    string? ScheduleMode,
    string? SourceType,
    string? SinkType
);

public static class EvaluationContractMetadataResolver
{
    public static EvaluationContractMetadata Resolve(ContractBase contract)
    {
        var scheduleMode = contract.Schedule?.Mode.ToString();

        return contract switch
        {
            ApiToApiContract => new EvaluationContractMetadata("ApiToApi", scheduleMode, "Api", "Api"),
            ApiToFileContract => new EvaluationContractMetadata("ApiToFile", scheduleMode, "Api", "File"),
            FileToApiContract => new EvaluationContractMetadata(
                "FileToApi",
                scheduleMode ?? ResolveLegacyIntervalMode(contract.IntervalSeconds),
                "File",
                "Api"),
            FileToFileContract => new EvaluationContractMetadata(
                "FileToFile",
                scheduleMode ?? ResolveLegacyIntervalMode(contract.IntervalSeconds),
                "File",
                "File"),
            PortToApiContract => new EvaluationContractMetadata("PortToApi", scheduleMode, "Port", "Api"),
            PortToFileContract => new EvaluationContractMetadata("PortToFile", scheduleMode, "Port", "File"),
            _ => new EvaluationContractMetadata(contract.GetType().Name, scheduleMode, null, null)
        };
    }

    private static string? ResolveLegacyIntervalMode(int intervalSeconds)
    {
        return intervalSeconds > 0 ? "Interval" : null;
    }
}
