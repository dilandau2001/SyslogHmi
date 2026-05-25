using System;
using System.Windows.Input;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Simple ICommand implementation that relays Execute and CanExecute to delegates.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// Tied into CommandManager.RequerySuggested for automatic reevaluation in WPF.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Creates a new RelayCommand.
        /// </summary>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute with the given parameter.
        /// </summary>
        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <summary>
        /// Executes the command action.
        /// </summary>
        public void Execute(object parameter) => _execute(parameter);
    }
}
