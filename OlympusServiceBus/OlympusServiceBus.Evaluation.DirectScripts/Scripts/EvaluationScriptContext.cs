namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public sealed class EvaluationScriptContext
{
    public EvaluationScriptContext(
        string baseDirectory,
        string outputDirectory,
        HttpClient httpClient)
    {
        BaseDirectory = baseDirectory;
        OutputDirectory = outputDirectory;
        HttpClient = httpClient;
    }

    public string BaseDirectory { get; }

    public string OutputDirectory { get; }

    public HttpClient HttpClient { get; }
}
