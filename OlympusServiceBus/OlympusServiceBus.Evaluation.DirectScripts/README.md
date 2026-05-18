# OlympusServiceBus.Evaluation.DirectScripts

This project is the direct-script baseline for the thesis evaluation.

Scripts in this project represent hardcoded one-off integration jobs that execute directly in C# without using the OlympusServiceBus engine execution pipeline. They are intended to be compared against OlympusServiceBus configuration-driven mediation flows during the evaluation.

The project now includes a file-based evaluation recording service with the same session and job-record schema used by the ESB Engine:

- `active-session.json`
- `sessions/<sessionId>/session.json`
- `sessions/<sessionId>/records/*.json`
- CSV export with the same column layout as the engine

By default, direct-script recordings are stored under `%AppData%\\OlympusServiceBus\\EvaluationRecording\\DirectScripts` so they do not clash with an engine-managed active session. To force the exact same storage root as the engine, set `OLYMPUS_EVALUATION_RECORDING_ROOT` or `OLYMPUS_DIRECT_SCRIPTS_EVALUATION_RECORDING_ROOT`.

To add a new baseline script:

1. Implement `IEvaluationScript`.
2. Place the implementation in `Scenarios`.
3. Register it in `ScriptRegistry`.

`Program.cs` is the central runner. It creates the shared execution context, lists available scripts, selects the target script, runs it, prints the execution summary, and records the run when an evaluation recording session is active.

Scripts can hardcode multiple API calls, file operations, or batches inside a single run. `EvaluationOperationTracker` is available to aggregate operation counts, bytes processed, and average operation duration into `EvaluationMetrics`.

Example usage:

```bash
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- PlaceholderScript
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- --recording-status
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- --start-recording
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- --stop-recording
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- --export-recording <sessionId>
```
