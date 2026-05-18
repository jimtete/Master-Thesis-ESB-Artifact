using OlympusServiceBus.Evaluation.DirectScripts.Recording;

namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public sealed class EvaluationScriptContext
{
    public EvaluationScriptContext(
        string baseDirectory,
        string outputDirectory,
        HttpClient httpClient,
        IEvaluationRecordingService evaluationRecordingService,
        EvaluationRecordingSession? activeRecordingSession)
    {
        BaseDirectory = baseDirectory;
        OutputDirectory = outputDirectory;
        HttpClient = httpClient;
        EvaluationRecordingService = evaluationRecordingService;
        ActiveRecordingSession = activeRecordingSession;
    }

    public string BaseDirectory { get; }

    public string OutputDirectory { get; }

    public HttpClient HttpClient { get; }

    public IEvaluationRecordingService EvaluationRecordingService { get; }

    public EvaluationRecordingSession? ActiveRecordingSession { get; }
}
