namespace OlympusServiceBus.Engine.Execution.Transformation;

public interface IExpressionEvaluator
{
    bool TryEvaluateAssignments(
        string expression,
        decimal[] inputs,
        out Dictionary<int, decimal> outputs);
}