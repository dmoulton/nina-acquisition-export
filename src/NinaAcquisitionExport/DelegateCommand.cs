using System.Windows.Input;

namespace NinaAcquisitionExport;

public sealed class DelegateCommand : ICommand {
    private readonly Action execute;
    private readonly Func<bool> canExecute;

    public DelegateCommand(Action execute, Func<bool>? canExecute = null) {
        this.execute = execute;
        this.canExecute = canExecute ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute();

    public void Execute(object? parameter) => execute();
}
