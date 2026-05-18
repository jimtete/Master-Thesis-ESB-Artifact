namespace OlympusServiceBus.Evaluation.DirectScripts.Recording;

public interface IEvaluationRecordingService
{
    string StorageRootPath { get; }

    Task<EvaluationRecordingSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default);

    Task<EvaluationRecordingSession> StartSessionAsync(CancellationToken cancellationToken = default);

    Task<EvaluationRecordingSession?> StopSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task RecordJobAsync(EvaluationJobRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationJobRecord>> GetSessionRecordsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task ExportSessionToCsvAsync(
        string sessionId,
        string destinationFilePath,
        CancellationToken cancellationToken = default);
}
