using System;
using System.Windows.Input;

namespace ConfuserEx.Commanding {
	internal class RelayCommand : ICommand {
		readonly Action execute;
		readonly Func<bool> canExecute;

		public RelayCommand(Action execute, Func<bool> canExecute = null) {
			this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
			this.canExecute = canExecute;
		}

		public bool CanExecute(object parameter) {
			return canExecute == null || canExecute();
		}

		public void Execute(object parameter) {
			execute();
		}

		public event EventHandler CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
	}

	internal class RelayCommand<T> : ICommand {
		readonly Action<T> execute;
		readonly Func<T, bool> canExecute;

		public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null) {
			this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
			this.canExecute = canExecute;
		}

		public bool CanExecute(object parameter) {
			return canExecute == null || canExecute(ConvertParameter(parameter));
		}

		public void Execute(object parameter) {
			execute(ConvertParameter(parameter));
		}

		static T ConvertParameter(object parameter) {
			return parameter == null ? default(T) : (T)parameter;
		}

		public event EventHandler CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
	}
}
