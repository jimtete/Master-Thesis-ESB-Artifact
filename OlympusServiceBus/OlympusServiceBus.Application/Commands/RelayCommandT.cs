using System.Windows.Input;

namespace OlympusServiceBusApplication.Commands;

public class RelayCommandT<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommandT(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }
    
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_canExecute is null)
        {
            return true;
        }
        
        return _canExecute(ConvertParameter(parameter));
    }

    public void Execute(object? parameter)
    {
        _execute(ConvertParameter(parameter));
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T? ConvertParameter(object? parameter)
    {
        if (parameter is null)
        {
            return default;
        }

        if (parameter is T typedParameter)
        {
            return typedParameter;
        }
        
        throw new ArgumentException(
            $"Invalid command parameter type. Expected {typeof(T).Name}, got {parameter.GetType().Name}.");
    }
}