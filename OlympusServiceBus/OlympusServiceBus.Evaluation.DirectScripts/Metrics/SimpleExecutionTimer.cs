using System.Diagnostics;

namespace OlympusServiceBus.Evaluation.DirectScripts;

public sealed class SimpleExecutionTimer
{
    private readonly Stopwatch _stopwatch;

    private SimpleExecutionTimer()
    {
        _stopwatch = Stopwatch.StartNew();
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public static SimpleExecutionTimer StartNew()
    {
        return new SimpleExecutionTimer();
    }
}
