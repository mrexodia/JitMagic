using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JitMagic.Models;
using JitMagic.MVVMLibLite;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace JitMagic.ViewModels {

	public class JITSelectorViewModel : OurViewModelBase {

		public string WindowTitle {
			get; set => Set(ref field, value);
		} = "JIT Magic";

		public GridLength LeftCommandColWidth {
			get; set => Set(ref field, value);
		} = new GridLength(1, GridUnitType.Star);

		private ConfigManager config = new();
		private AEDebugManager aeDebug = new();
		private CLIManager cli;
		public JITSelectorViewModel() {
			var args = Environment.GetCommandLineArgs();
			var designMode = args.Length == 0 || (args[0].IndexOf("JitMagic.exe", StringComparison.CurrentCultureIgnoreCase) == -1 && args[0].Contains("VisualStudio"));
			config.ReadConfig();
			if (!designMode)
				cli = new(config, aeDebug, args.Skip(1).ToArray());

			ApplyConfig();
		}

		private void ApplyConfig() {
			if (config.Config.OverrideWidth > 100)
				WinWidth = config.Config.OverrideWidth;
			if (config.Config.OverrideHeight > 100)
				WinHeight = config.Config.OverrideHeight;

			IgnoreForMinutes = config.Config.DefaultIgnoreMinutes;

			// Re-apply theme if needed (though usually done in Loaded)
			// But since Loaded calls it, and we might want to update it immediately:
			ApplyTheme();
		}

		private void ApplyTheme() {
			var theme = config.Config.ForcedTheme?.Trim() ?? "";
#pragma warning disable WPF0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
			if (theme.Equals("light", StringComparison.CurrentCultureIgnoreCase))
				Application.Current.ThemeMode = ThemeMode.Light;
			if (theme.Equals("dark", StringComparison.CurrentCultureIgnoreCase))
				Application.Current.ThemeMode = ThemeMode.Dark;
			else if (string.IsNullOrEmpty(theme))
				Application.Current.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
		}

		public event EventHandler<ConfigManager> OpenSettingsRequested;
		public OurCommand OpenSettingsCmd => GetOurCmdSync(OpenSettings);
		public void OpenSettings() {
			OpenSettingsRequested?.Invoke(this, config);
			// After returning from modal dialog:
			config.ReadConfig(); // Reload in case it was changed on disk or just to be safe
			ApplyConfig();
		}

		public bool TopMost {
			get; set => Set(ref field, value);
		}
		public void Loaded() {
			if (cli != null && !new[] { APP_ACTION.None, APP_ACTION.AEDebug, APP_ACTION.Screenshot }.Contains(cli.mode))
				Close();

			ApplyTheme();


			var fallback = Icon.ExtractAssociatedIcon(ProcHelper.GetCurrentExecutionPath());
			if (cli.mode == APP_ACTION.AEDebug) {
				TopMost = !Debugger.IsAttached;
				WindowTitle += $" - PID: {cli.target.Pid}";
				pid = cli.target.Pid;
				processPath = cli.target.ProcPath;
				ProcessInfo = $"{Path.GetFileName(processPath)} ({cli.target.Architecture})";
				StandardLaunchOnlyVisibility = false;
			} else if (cli.mode == APP_ACTION.Screenshot) {
				AEDebugOnlyVisibility = false;
				WindowTitle += $" - PID: 4";
				ProcessInfo = $"lsass.exe (x86)";
			} else {
				LeftCommandColWidth = new GridLength(0);
				AEDebugOnlyVisibility = false;
				AttachText = "Launch";
			}

			foreach (var debugger in config.Config.JitDebuggers) {
				if ((cli.mode == APP_ACTION.AEDebug && debugger.Architecture.HasFlag(cli.target.Architecture) == false) || !debugger.Exists)
					continue;
				debugger.LoadIcon(fallback);
				debuggers.Add(debugger);
			}
			selected_debugger = debuggers.FirstOrDefault();
			if (cli.mode == APP_ACTION.AEDebug)
				WatchForExit();
		}

		private async void WatchForExit() {
			while (lastKnownStatus == CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting) {
				CheckProcessStillRunning();
				await Task.Delay(1000);

			}

		}

		public OurCommand LaunchNormalCmd => GetOurCmdSync(LaunchNormal);
		public void LaunchNormal() {
			TopMost = false;
			ProcHelper.LaunchUs(false);
		}

		public int pid {
			get; set => Set(ref field, value);
		}

		public string ProcessInfo {
			get; set => Set(ref field, value);
		} = "No Process Loaded";


		public OurCommand IgnoreAllCmd => GetOurCmdSync(IgnoreAll);
		public void IgnoreAll() {
			config.Config.IgnoringUntil = DateTime.Now.AddMinutes(IgnoreForMinutes);
			config.SaveConfig();
			Close();
		}


		public int WinHeight {
			get; set => Set(ref field, value);
		} = 220;

		public int WinWidth {
			get; set => Set(ref field, value);
		} = 1000;


		public OurCommand CloseCmd => GetOurCmdSync(Close);

		public void Close() {
			HideWin?.Invoke(this, null);
			if (cli.mode == APP_ACTION.AEDebug)
				aeDebug.SilentExit(cli.target.Process, DebuggerAttached || config.Config.DontKillTargetProcessOnNonDebugExit);//never kill the target once we handed it off to a debugger, that is not a 'non-debug exit'
			else
				Environment.Exit(0);

		}
		public event EventHandler FocusAutoExitNowBtn;
		public event EventHandler HideWin;

		public int IgnoreForMinutes {
			get; set => Set(ref field, value);
		}

		public OurCommand SaveWindowSizeCmd => GetOurCmdSync(SaveWindowSize);
		public void SaveWindowSize() {

			config.Config.OverrideWidth = WinWidth;
			config.Config.OverrideHeight = WinHeight;
			config.SaveConfig();
		}
		public OurCommand AttachCmd => GetOurCmd(Attach);
		public async Task Attach() {
			var debugger = selected_debugger;
			if (debugger == null)
				return;
			CheckProcessStillRunning();
			var shouldAutoClose = cli.target.Status == CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting;
			if (shouldAutoClose) // if we are in 'launcher' mode then we dont hide ourselves
				HideWin?.Invoke(this, null);
			await Task.Delay(10);
			aeDebug.StartDebugger(debugger, pid, cli.target?.JitDebugStructPtrAddy);
			DebuggerAttached = true;
			if (shouldAutoClose) // if we are in 'launcher' mode then we dont close
				DelayClose(debugger.AdditionalDelaySecs);
		}
		private bool DebuggerAttached;
		private async void DelayClose(int extraDelaySecs) {
			if (extraDelaySecs > 0)
				await Task.Delay(TimeSpan.FromSeconds(extraDelaySecs));
			aeDebug.SignalResume();
			Close();
		}
		CLIManager.RequestedTargetProc.TARGET_STATUS lastKnownStatus = CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting;
		public void CheckProcessStillRunning() {
			if (DebuggerAttached || cli.mode != APP_ACTION.AEDebug || lastKnownStatus != CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting)
				return;
			var status = cli.target.Status;
			if (status == lastKnownStatus)
				return;
			lastKnownStatus = status;
			RaisePropertyChanged(() => ProcessExitedEarlyOverlayVisible);
			RaisePropertyChanged(() => ProcessExitedEarlyOverlayText);

			if (status != CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting && config.Config.OnProcDieAutoCloseAfterSecs != null)
				AutoExit(config.Config.OnProcDieAutoCloseAfterSecs.Value);

			FocusAutoExitNowBtn?.Invoke(this, null);
		}

		private async void AutoExit(int secs) {
			while (secs > 0) {
				OnProcDieAutoCloseAfterSecs = secs--;
				await Task.Delay(TimeSpan.FromSeconds(1));
			}
			Close();
		}

		public OurCommand BlacklistAppCmd => GetOurCmdSync(BlacklistApp);
		public void BlacklistApp() {

			var confirm = MessageBox.Show($"Are you sure you want to blacklist the executable path: {processPath} from future debugging? The only way to undo this is to manually edit the JitMagic.json file", $"Confirm Blacklist {Path.GetFileName(processPath)}", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
			if (confirm != MessageBoxResult.Yes)
				return;

			config.Config.BlacklistedPaths.Add(processPath);
			config.SaveConfig();
			aeDebug.SilentExit(cli.target.Process, config.Config.DontKillTargetProcessOnNonDebugExit || config.Config.DontKillBlacklistedProcesses);
		}

		public string processPath {
			get; set => Set(ref field, value);
		}


		public string AttachText {
			get; set => Set(ref field, value);
		} = "Attach";

		public bool AEDebugOnlyVisibility {
			get; set => Set(ref field, value);
		} = true;


		public bool StandardLaunchOnlyVisibility {
			get; set => Set(ref field, value);
		} = true;

		public OurCommand RemoveAsJITCmd => GetOurCmdSync(RemoveAsJIT);
		public void RemoveAsJIT() {
			aeDebug.UpdateRegistration(APP_ACTION.Unregister);
		}

		public OurCommand DebuggerDoubleClickedCmd => GetOurCmd(DebuggerDoubleClicked);
		public async Task DebuggerDoubleClicked() {
			await Task.Delay(10);//make sure it has time to updated selected
			await AttachCmd.Execute();
		}
		public OurCommand RemoveSelectedDebuggerCmd => GetOurCmdSync(RemoveSelectedDebugger);
		public void RemoveSelectedDebugger() {
			if (selected_debugger == null)
				return;
			config.RemoveDebugger(selected_debugger.Name);
			debuggers.Remove(selected_debugger);
			selected_debugger = debuggers.FirstOrDefault();
		}

		public JitDebugger selected_debugger {
			get; set => Set(ref field, value);
		}

		public bool ProcessExitedEarlyOverlayVisible => cli.mode != APP_ACTION.AEDebug || cli.target.Status == CLIManager.RequestedTargetProc.TARGET_STATUS.Waiting ? false : true;

		public string ProcessExitedEarlyOverlayText {
			get {
				if (cli.mode != APP_ACTION.AEDebug)
					return "";
				if (cli.target.Status == CLIManager.RequestedTargetProc.TARGET_STATUS.NotFound)
					return "Target process from -pid not found (or exited fast)";

				return "Target process exited before a debugger was attached (generally something external killed it)";

			}
		}

		public bool AutoExitWarningTextVisibile => config.Config.OnProcDieAutoCloseAfterSecs == null ? false : true;
		public int AutoExitAfterSecs => config.Config.OnProcDieAutoCloseAfterSecs ?? 0;

		public int OnProcDieAutoCloseAfterSecs {
			get; set => Set(ref field, value);
		}


		public ObservableCollection<JitDebugger> debuggers {
			get; set => Set(ref field, value);
		} = new();


	}
}
