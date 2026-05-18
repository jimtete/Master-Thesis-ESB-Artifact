# OlympusServiceBus.Evaluation.DirectScripts

This project is the direct-script baseline for the thesis evaluation.

Scripts in this project represent hardcoded one-off integration jobs that execute directly in C# without using the OlympusServiceBus engine execution pipeline. They are intended to be compared against OlympusServiceBus configuration-driven mediation flows during the evaluation.

To add a new baseline script:

1. Implement `IEvaluationScript`.
2. Place the implementation in `Scenarios`.
3. Register it in `ScriptRegistry`.

`Program.cs` is the central runner. It creates the shared execution context, lists available scripts, selects the target script, runs it, and prints the execution summary.

Example usage:

```bash
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts
dotnet run --project OlympusServiceBus.Evaluation.DirectScripts -- PlaceholderScript
```
