using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OlympusServiceBus.Evaluation.DirectScripts.Recording;

public sealed class FileEvaluationRecordingService : IEvaluationRecordingService
{
    private const string ActiveSessionFileName = "active-session.json";
    private const string SessionMetadataFileName = "session.json";
    private const string DirectScriptsStorageRootEnvironmentVariableName = "OLYMPUS_DIRECT_SCRIPTS_EVALUATION_RECORDING_ROOT";
    private const string SharedStorageRootEnvironmentVariableName = "OLYMPUS_EVALUATION_RECORDING_ROOT";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileEvaluationRecordingService()
    {
        StorageRootPath = ResolveStorageRootPath();
    }

    public string StorageRootPath { get; }

    public async Task<EvaluationRecordingSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var activeSessionPath = GetActiveSessionPath();
        if (!File.Exists(activeSessionPath))
        {
            return null;
        }

        return await ReadJsonAsync<EvaluationRecordingSession>(activeSessionPath, cancellationToken);
    }

    public async Task<EvaluationRecordingSession> StartSessionAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StorageRootPath);

        var existingSession = await GetActiveSessionAsync(cancellationToken);
        if (existingSession is not null)
        {
            throw new InvalidOperationException(
                $"Recording session '{existingSession.SessionId}' is already active.");
        }

        var session = new EvaluationRecordingSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        Directory.CreateDirectory(GetSessionDirectoryPath(session.SessionId));
        Directory.CreateDirectory(GetSessionRecordsDirectoryPath(session.SessionId));

        await WriteJsonAtomicallyAsync(
            GetSessionMetadataPath(session.SessionId),
            session,
            cancellationToken);

        await WriteJsonAtomicallyAsync(
            GetActiveSessionPath(),
            session,
            cancellationToken);

        return session;
    }

    public async Task<EvaluationRecordingSession?> StopSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var activeSession = await GetActiveSessionAsync(cancellationToken);
        if (activeSession is null || !string.Equals(activeSession.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
        {
            var persistedSession = await TryGetSessionAsync(sessionId, cancellationToken);
            if (persistedSession is null)
            {
                return null;
            }

            if (persistedSession.StoppedAtUtc is null)
            {
                persistedSession.StoppedAtUtc = DateTimeOffset.UtcNow;
                await WriteJsonAtomicallyAsync(
                    GetSessionMetadataPath(sessionId),
                    persistedSession,
                    cancellationToken);
            }

            return persistedSession;
        }

        activeSession.StoppedAtUtc = DateTimeOffset.UtcNow;

        await WriteJsonAtomicallyAsync(
            GetSessionMetadataPath(sessionId),
            activeSession,
            cancellationToken);

        var activeSessionPath = GetActiveSessionPath();
        if (File.Exists(activeSessionPath))
        {
            File.Delete(activeSessionPath);
        }

        return activeSession;
    }

    public async Task RecordJobAsync(EvaluationJobRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.RecordingSessionId))
        {
            throw new ArgumentException("RecordingSessionId is required.", nameof(record));
        }

        var recordsDirectoryPath = GetSessionRecordsDirectoryPath(record.RecordingSessionId);
        Directory.CreateDirectory(recordsDirectoryPath);

        var fileName = $"{record.StartTimestampUtc:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        var recordPath = Path.Combine(recordsDirectoryPath, fileName);

        await WriteJsonAtomicallyAsync(recordPath, record, cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationJobRecord>> GetSessionRecordsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var recordsDirectoryPath = GetSessionRecordsDirectoryPath(sessionId);
        if (!Directory.Exists(recordsDirectoryPath))
        {
            return Array.Empty<EvaluationJobRecord>();
        }

        var recordPaths = Directory.EnumerateFiles(recordsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var records = new List<EvaluationJobRecord>(recordPaths.Count);

        foreach (var recordPath in recordPaths)
        {
            var record = await ReadJsonAsync<EvaluationJobRecord>(recordPath, cancellationToken);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records
            .OrderBy(record => record.StartTimestampUtc)
            .ThenBy(record => record.ContractId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task ExportSessionToCsvAsync(
        string sessionId,
        string destinationFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("Destination path is required.", nameof(destinationFilePath));
        }

        var session = await TryGetSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException($"Recording session '{sessionId}' was not found.");
        }

        var records = await GetSessionRecordsAsync(sessionId, cancellationToken);

        var directoryPath = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var csv = BuildCsv(records);
        await File.WriteAllTextAsync(destinationFilePath, csv, Encoding.UTF8, cancellationToken);
    }

    private async Task<EvaluationRecordingSession?> TryGetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var sessionMetadataPath = GetSessionMetadataPath(sessionId);
        if (!File.Exists(sessionMetadataPath))
        {
            return null;
        }

        return await ReadJsonAsync<EvaluationRecordingSession>(sessionMetadataPath, cancellationToken);
    }

    private string GetActiveSessionPath()
    {
        return Path.Combine(StorageRootPath, ActiveSessionFileName);
    }

    private string GetSessionDirectoryPath(string sessionId)
    {
        return Path.Combine(StorageRootPath, "sessions", sessionId);
    }

    private string GetSessionMetadataPath(string sessionId)
    {
        return Path.Combine(GetSessionDirectoryPath(sessionId), SessionMetadataFileName);
    }

    private string GetSessionRecordsDirectoryPath(string sessionId)
    {
        return Path.Combine(GetSessionDirectoryPath(sessionId), "records");
    }

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            tempPath,
            JsonSerializer.Serialize(value, JsonOptions),
            Encoding.UTF8,
            cancellationToken);

        File.Move(tempPath, path, true);
    }

    private static string BuildCsv(IReadOnlyList<EvaluationJobRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "RecordingSessionId,ContractId,ContractName,ContractType,ScheduleMode,TriggerType,SourceType,SinkType,StartTimestampUtc,EndTimestampUtc,DurationMilliseconds,Status,ErrorMessage,ProcessedRowsOrMessagesCount");

        foreach (var record in records)
        {
            builder.AppendLine(string.Join(",",
                EscapeCsv(record.RecordingSessionId),
                EscapeCsv(record.ContractId),
                EscapeCsv(record.ContractName),
                EscapeCsv(record.ContractType),
                EscapeCsv(record.ScheduleMode),
                EscapeCsv(record.TriggerType),
                EscapeCsv(record.SourceType),
                EscapeCsv(record.SinkType),
                EscapeCsv(record.StartTimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                EscapeCsv(record.EndTimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                EscapeCsv(record.DurationMilliseconds.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(record.Status),
                EscapeCsv(record.ErrorMessage),
                EscapeCsv(record.ProcessedRowsOrMessagesCount?.ToString(CultureInfo.InvariantCulture))));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string ResolveStorageRootPath()
    {
        var directScriptsPath = Environment.GetEnvironmentVariable(DirectScriptsStorageRootEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(directScriptsPath))
        {
            return directScriptsPath;
        }

        var sharedPath = Environment.GetEnvironmentVariable(SharedStorageRootEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(sharedPath))
        {
            return sharedPath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OlympusServiceBus",
            "EvaluationRecording",
            "DirectScripts");
    }
}
