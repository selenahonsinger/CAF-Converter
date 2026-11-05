using System.Windows.Input;
using System.Windows;

namespace CafConverter.Commands;

public sealed class AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) : ICommand
{
    private bool _executing;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_executing && (canExecute?.Invoke(parameter) ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _executing = true; RaiseCanExecuteChanged();
        try { await execute(parameter); }
        finally { _executing = false; RaiseCanExecuteChanged(); }
    }
    public void RaiseCanExecuteChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(new Action(RaiseCanExecuteChanged));
            return;
        }
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
