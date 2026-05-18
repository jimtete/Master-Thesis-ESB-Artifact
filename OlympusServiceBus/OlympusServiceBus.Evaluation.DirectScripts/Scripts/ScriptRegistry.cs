using OlympusServiceBus.Evaluation.DirectScripts.Scenarios;

namespace OlympusServiceBus.Evaluation.DirectScripts.Scripts;

public sealed class ScriptRegistry
{
    private readonly Dictionary<string, IEvaluationScript> _scripts;

    public ScriptRegistry()
    {
        _scripts = new Dictionary<string, IEvaluationScript>(StringComparer.OrdinalIgnoreCase);

        Register(new PlaceholderScript());
    }

    public static string DefaultScriptName => PlaceholderScript.ScriptName;

    public IReadOnlyCollection<IEvaluationScript> AvailableScripts => _scripts.Values;

    public bool TryGetByName(string scriptName, out IEvaluationScript script)
    {
        return _scripts.TryGetValue(scriptName, out script!);
    }

    private void Register(IEvaluationScript script)
    {
        _scripts[script.Name] = script;
    }
}
