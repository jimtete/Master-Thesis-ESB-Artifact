using System.Globalization;

namespace OlympusServiceBus.Engine.Execution.Transformation;

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    private enum ExpressionTokenType
    {
        Number,
        InputVariable,
        OutputVariable,
        Operator,
        LeftParenthesis,
        RightParenthesis
    }

    private readonly record struct ExpressionToken(ExpressionTokenType Type, string Value);

    public bool TryEvaluateAssignments(
        string expression,
        decimal[] inputs,
        out Dictionary<int, decimal> outputs)
    {
        outputs = new Dictionary<int, decimal>();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var normalized = expression
            .Replace('[', '(')
            .Replace(']', ')');

        var statements = normalized
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (statements.Length == 0)
        {
            return false;
        }

        // Convenience:
        // If the user writes only an expression, treat it as an assignment to $o_0.
        var hasExplicitAssignment = statements.Any(static s => s.Contains('='));
        if (!hasExplicitAssignment)
        {
            if (!TryEvaluateArithmeticExpression(statements[0], inputs, outputs, out var implicitResult))
            {
                return false;
            }

            outputs[0] = implicitResult;
            return true;
        }

        foreach (var statement in statements)
        {
            var equalsIndex = statement.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == statement.Length - 1)
            {
                return false;
            }

            var left = statement[..equalsIndex].Trim();
            var right = statement[(equalsIndex + 1)..].Trim();

            if (!TryParseOutputReference(left, out var outputIndex))
            {
                return false;
            }

            if (!TryEvaluateArithmeticExpression(right, inputs, outputs, out var result))
            {
                return false;
            }

            outputs[outputIndex] = result;
        }

        return true;
    }

    private static bool TryEvaluateArithmeticExpression(
        string expression,
        decimal[] inputs,
        IReadOnlyDictionary<int, decimal> outputs,
        out decimal result)
    {
        result = 0m;

        if (!TryTokenize(expression, out var tokens))
        {
            return false;
        }

        if (!TryConvertToReversePolishNotation(tokens, out var rpn))
        {
            return false;
        }

        return TryEvaluateReversePolishNotation(rpn, inputs, outputs, out result);
    }

    private static bool TryTokenize(string expression, out List<ExpressionToken> tokens)
    {
        tokens = new List<ExpressionToken>();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var i = 0;

        while (i < expression.Length)
        {
            var c = expression[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                var dotCount = 0;

                while (i < expression.Length &&
                       (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    if (expression[i] == '.')
                    {
                        dotCount++;
                        if (dotCount > 1)
                        {
                            return false;
                        }
                    }

                    i++;
                }

                var numberText = expression[start..i];
                if (!decimal.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return false;
                }

                tokens.Add(new ExpressionToken(ExpressionTokenType.Number, numberText));
                continue;
            }

            if (c == '$')
            {
                var start = i;
                i++;

                while (i < expression.Length &&
                       (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                var variable = expression[start..i];

                if (variable.StartsWith("$i_", StringComparison.Ordinal))
                {
                    if (!int.TryParse(variable[3..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        return false;
                    }

                    tokens.Add(new ExpressionToken(ExpressionTokenType.InputVariable, variable));
                    continue;
                }

                if (variable.StartsWith("$o_", StringComparison.Ordinal))
                {
                    if (!int.TryParse(variable[3..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        return false;
                    }

                    tokens.Add(new ExpressionToken(ExpressionTokenType.OutputVariable, variable));
                    continue;
                }

                return false;
            }

            switch (c)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '^':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Operator, c.ToString()));
                    i++;
                    continue;

                case '(':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.LeftParenthesis, "("));
                    i++;
                    continue;

                case ')':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.RightParenthesis, ")"));
                    i++;
                    continue;

                default:
                    return false;
            }
        }

        return tokens.Count > 0;
    }

    private static bool TryConvertToReversePolishNotation(
        IReadOnlyList<ExpressionToken> tokens,
        out List<ExpressionToken> output)
    {
        output = new List<ExpressionToken>();
        var operators = new Stack<ExpressionToken>();
        ExpressionToken? previousToken = null;

        foreach (var rawToken in tokens)
        {
            var token = rawToken;

            switch (token.Type)
            {
                case ExpressionTokenType.Number:
                case ExpressionTokenType.InputVariable:
                case ExpressionTokenType.OutputVariable:
                    output.Add(token);
                    previousToken = token;
                    break;

                case ExpressionTokenType.Operator:
                {
                    if ((token.Value == "-" || token.Value == "+") &&
                        (previousToken is null ||
                         previousToken.Value.Type == ExpressionTokenType.Operator ||
                         previousToken.Value.Type == ExpressionTokenType.LeftParenthesis))
                    {
                        token = new ExpressionToken(ExpressionTokenType.Operator, token.Value == "-" ? "u-" : "u+");
                    }

                    while (operators.Count > 0 &&
                           operators.Peek().Type == ExpressionTokenType.Operator &&
                           ShouldPopBeforePushing(token, operators.Peek()))
                    {
                        output.Add(operators.Pop());
                    }

                    operators.Push(token);
                    previousToken = token;
                    break;
                }

                case ExpressionTokenType.LeftParenthesis:
                    operators.Push(token);
                    previousToken = token;
                    break;

                case ExpressionTokenType.RightParenthesis:
                {
                    var matchedLeftParenthesis = false;

                    while (operators.Count > 0)
                    {
                        var top = operators.Pop();

                        if (top.Type == ExpressionTokenType.LeftParenthesis)
                        {
                            matchedLeftParenthesis = true;
                            break;
                        }

                        output.Add(top);
                    }

                    if (!matchedLeftParenthesis)
                    {
                        return false;
                    }

                    previousToken = token;
                    break;
                }

                default:
                    return false;
            }
        }

        while (operators.Count > 0)
        {
            var top = operators.Pop();

            if (top.Type is ExpressionTokenType.LeftParenthesis or ExpressionTokenType.RightParenthesis)
            {
                return false;
            }

            output.Add(top);
        }

        return output.Count > 0;
    }

    private static bool ShouldPopBeforePushing(ExpressionToken incoming, ExpressionToken topOfStack)
    {
        var incomingPrecedence = GetOperatorPrecedence(incoming.Value);
        var stackPrecedence = GetOperatorPrecedence(topOfStack.Value);

        if (IsRightAssociative(incoming.Value))
        {
            return incomingPrecedence < stackPrecedence;
        }

        return incomingPrecedence <= stackPrecedence;
    }

    private static int GetOperatorPrecedence(string op) => op switch
    {
        "+" or "-" => 1,
        "*" or "/" or "%" => 2,
        "u-" or "u+" => 3,
        "^" => 4,
        _ => throw new InvalidOperationException($"Unknown operator '{op}'.")
    };

    private static bool IsRightAssociative(string op) => op is "^" or "u-" or "u+";

    private static bool TryEvaluateReversePolishNotation(
        IReadOnlyList<ExpressionToken> tokens,
        decimal[] inputs,
        IReadOnlyDictionary<int, decimal> outputs,
        out decimal result)
    {
        result = 0m;
        var stack = new Stack<decimal>();

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case ExpressionTokenType.Number:
                    stack.Push(decimal.Parse(token.Value, CultureInfo.InvariantCulture));
                    break;

                case ExpressionTokenType.InputVariable:
                {
                    if (!int.TryParse(token.Value[3..], NumberStyles.None, CultureInfo.InvariantCulture, out var inputIndex))
                    {
                        return false;
                    }

                    if (inputIndex < 0 || inputIndex >= inputs.Length)
                    {
                        return false;
                    }

                    stack.Push(inputs[inputIndex]);
                    break;
                }

                case ExpressionTokenType.OutputVariable:
                {
                    if (!int.TryParse(token.Value[3..], NumberStyles.None, CultureInfo.InvariantCulture, out var outputIndex))
                    {
                        return false;
                    }

                    if (!outputs.TryGetValue(outputIndex, out var outputValue))
                    {
                        return false;
                    }

                    stack.Push(outputValue);
                    break;
                }

                case ExpressionTokenType.Operator:
                {
                    if (token.Value is "u-" or "u+")
                    {
                        if (stack.Count < 1)
                        {
                            return false;
                        }

                        var value = stack.Pop();
                        stack.Push(token.Value == "u-" ? -value : value);
                        break;
                    }

                    if (stack.Count < 2)
                    {
                        return false;
                    }

                    var right = stack.Pop();
                    var left = stack.Pop();

                    if (!TryApplyBinaryOperator(left, right, token.Value, out var operationResult))
                    {
                        return false;
                    }

                    stack.Push(operationResult);
                    break;
                }

                default:
                    return false;
            }
        }

        if (stack.Count != 1)
        {
            return false;
        }

        result = stack.Pop();
        return true;
    }

    private static bool TryApplyBinaryOperator(decimal left, decimal right, string op, out decimal result)
    {
        result = 0m;

        try
        {
            switch (op)
            {
                case "+":
                    result = left + right;
                    return true;

                case "-":
                    result = left - right;
                    return true;

                case "*":
                    result = left * right;
                    return true;

                case "/":
                    if (right == 0m)
                    {
                        return false;
                    }

                    result = left / right;
                    return true;

                case "%":
                    if (right == 0m)
                    {
                        return false;
                    }

                    result = left % right;
                    return true;

                case "^":
                {
                    var pow = Math.Pow((double)left, (double)right);

                    if (double.IsNaN(pow) || double.IsInfinity(pow))
                    {
                        return false;
                    }

                    result = (decimal)pow;
                    return true;
                }

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseOutputReference(string value, out int outputIndex)
    {
        outputIndex = -1;

        if (!value.StartsWith("$o_", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(
            value[3..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out outputIndex);
    }
}