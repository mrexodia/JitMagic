using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace JitMagic.MVVMLibLite {
	public class OurCommand : ICommand {
		private static Task T(Action action) {
			try {
				action();
				return Task.CompletedTask;
			} catch (Exception exception) {
				return Task.FromException(exception);
			}
		}
		public TimeSpan CommandMinRunTime = TimeSpan.FromSeconds(1);
		public bool CanExecute(object parameter) => (!auto_disable || !_running) && enabled;
		public OurCommand(Func<Task> action, bool auto_disable = true) {
			async_action = action;
			this.auto_disable = auto_disable;
		}
		public OurCommand(Action action, bool auto_disable, bool in_background) {
			if (in_background)
				async_action = () => Task.Run(action);
			else
				async_action = () => T(action);
			this.auto_disable = auto_disable;
		}
		private bool running {
			set {
				if (_running == value)
					return;
				_running = value;
				CanExecuteChanged?.Invoke(this, null);
			}
		}
		private bool _running;
		public bool enabled {
			get { return _enabled; }
			set {
				if (_enabled == value)
					return;
				_enabled = value;
				CanExecuteChanged?.Invoke(this, null);
			}
		}
		private bool _enabled = true;
		public event EventHandler CanExecuteChanged;
		public Func<Task> async_action;

		private readonly bool auto_disable;
		public void Execute(object parameter) {
#pragma warning disable 4014
			Execute();
#pragma warning restore 4014
		}
		public async Task Execute() {
			if (!enabled)
				return;
			running = true;
			var start_time = DateTime.MinValue;
			try {
				if (auto_disable)
					start_time = DateTime.Now;
				await async_action.Invoke();
			} catch (Exception e) {
				Debug.WriteLine($"Unhandled command exception of: {e}");
			} finally {
				if (auto_disable) {
					var diff = DateTime.Now - start_time;
					if (diff < CommandMinRunTime)
						await Task.Delay(CommandMinRunTime - diff);
				}
				running = false;
			}
		}
	}
}