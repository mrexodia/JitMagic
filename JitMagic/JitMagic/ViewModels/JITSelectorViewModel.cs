using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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
			get => _WindowTitle;
			set => Set(ref _WindowTitle, value);
		}
		private string _WindowTitle = "JIT Magic";
		public GridLength LeftCommandColWidth {
			get => _LeftCommandColWidth;
			set => Set(ref _LeftCommandColWidth, value);
		}
		private GridLength _LeftCommandColWidth = new GridLength(1, GridUnitType.Star);

		private ConfigManager config = new();
		private AEDebugManager aeDebug = new();
		private CLIManager cli;
		public JITSelectorViewModel() {
			var args = Environment.GetCommandLineArgs();
			var designMode = args.Length == 0 || (args[0].IndexOf("JitMagic.exe", StringComparison.CurrentCultureIgnoreCase) == -1 && args[0].Contains("VisualStudio"));
			config.ReadConfig();
			if (!designMode)
				cli = new(config, aeDebug, args.Skip(1).ToArray());


			if (config.Config.OverrideWidth > 100)
				WinWidth = config.Config.OverrideWidth;
			if (config.Config.OverrideHeight > 100)
				WinHeight = config.Config.OverrideHeight;

			IgnoreForMinutes = config.Config.DefaultIgnoreMinutes;
		}
		public bool TopMost {
			get => _TopMost;
			set => Set(ref _TopMost, value);
		}
		private bool _TopMost;
		public void Loaded() {
			if (cli != null && !new[] { APP_ACTION.None, APP_ACTION.AEDebug, APP_ACTION.Screenshot }.Contains(cli.mode))
				Close();

			var fallback = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
			if (cli.mode == APP_ACTION.AEDebug) {
				StandardLaunchOnlyVisibility = Visibility.Collapsed;
				TopMost = !Debugger.IsAttached;
				WindowTitle += $" - PID: {cli.target.Pid}";
				pid = cli.target.Pid;
				processPath = cli.target.ProcPath;
				ProcessInfo = $"{Path.GetFileName(processPath)} ({cli.target.Architecture})";
			} else if (cli.mode == APP_ACTION.Screenshot){
				AEDebugOnlyVisibility = Visibility.Collapsed;
				WindowTitle += $" - PID: 4";
				ProcessInfo = $"lsass.exe (x86)";
			} else {
				LeftCommandColWidth = new GridLength( 0);
				AEDebugOnlyVisibility = Visibility.Collapsed;
				AttachText = "Launch";
			}
			foreach (var debugger in config.Config.JitDebuggers) {
				if ((cli.mode == APP_ACTION.AEDebug && debugger.Architecture.HasFlag(cli.target.Architecture) == false) || !debugger.Exists)
					continue;
				debugger.LoadIcon(fallback);
				debuggers.Add(debugger);
			}
			selected_debugger = debuggers.FirstOrDefault();
		}
		public OurCommand LaunchNormalCmd => GetOurCmdSync(LaunchNormal);
		public void LaunchNormal() {
			TopMost = false;
			ProcHelper.LaunchUs(false);
		}

		public int pid {
			get => _pid;
			set => Set(ref _pid, value);
		}
		private int _pid;

		public string ProcessInfo {
			get => _ProcessInfo;
			set => Set(ref _ProcessInfo, value);
		}
		private string _ProcessInfo = "No Process Loaded";


		public OurCommand IgnoreAllCmd => GetOurCmdSync(IgnoreAll);
		public void IgnoreAll() {
			config.Config.IgnoringUntil = DateTime.Now.AddMinutes(IgnoreForMinutes);
			config.SaveConfig();
			Close();
		}


		public int WinHeight {
			get => _WinHeight;
			set => Set(ref _WinHeight, value);
		}
		private int _WinHeight = 210;

		public int WinWidth {
			get => _WinWidth;
			set => Set(ref _WinWidth, value);
		}
		private int _WinWidth = 1000;

		public void Close() {
			aeDebug.SignalResume();
			CloseWin?.Invoke(this, null);
		}
		public event EventHandler CloseWin;
		public event EventHandler HideWin;

		public int IgnoreForMinutes {
			get => _IgnoreForMinutes;
			set => Set(ref _IgnoreForMinutes, value);
		}
		private int _IgnoreForMinutes;

		public OurCommand SaveWindowSizeCmd => GetOurCmdSync(SaveWindowSize);
		public void SaveWindowSize() {

			config.Config.OverrideWidth = WinWidth;
			config.Config.OverrideHeight = WinHeight;
			config.SaveConfig();
		}
		public OurCommand AttachCmd => GetOurCmdSync(Attach);
		public void Attach() {
			var debugger = selected_debugger;
			if (debugger == null)
				return;
			if (pid != 0)
				HideWin?.Invoke(this, null);
			aeDebug.StartDebugger(debugger, pid, cli.target?.JitDebugStructPtrAddy);
			if (pid != 0)
				DelayClose(debugger.AdditionalDelaySecs);
		}
		private async void DelayClose(int extraDelaySecs) {
			if (extraDelaySecs > 0)
				await Task.Delay(TimeSpan.FromSeconds(extraDelaySecs));
			Close();
		}
		public OurCommand BlacklistAppCmd => GetOurCmdSync(BlacklistApp);
		public void BlacklistApp() {
			var confirm = MessageBox.Show($"Are you sure you want to blacklist the executable path: {processPath} from future debugging? The only way to undo this is to manually edit the JitMagic.json file", $"Confirm Blacklist {Path.GetFileName(processPath)}", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
			if (confirm != MessageBoxResult.Yes)
				return;
			config.Config.BlacklistedPaths.Add(processPath);
			config.SaveConfig();
			Close();
		}

		public string processPath {
			get => _processPath;
			set => Set(ref _processPath, value);
		}
		private string _processPath;


		public string AttachText {
			get => _AttachText;
			set => Set(ref _AttachText, value);
		}
		private string _AttachText = "Attach";


		public Visibility AEDebugOnlyVisibility {
			get => _AEDebugOnlyVisibility;
			set => Set(ref _AEDebugOnlyVisibility, value);
		}
		private Visibility _AEDebugOnlyVisibility = Visibility.Visible;


		public Visibility StandardLaunchOnlyVisibility {
			get => _StandardLaunchOnlyVisibility;
			set => Set(ref _StandardLaunchOnlyVisibility, value);
		}
		private Visibility _StandardLaunchOnlyVisibility = Visibility.Visible;


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
			get => _selected_debugger;
			set => Set(ref _selected_debugger, value);
		}
		private JitDebugger _selected_debugger;


		public ObservableCollection<JitDebugger> debuggers {
			get => _debuggers;
			set => Set(ref _debuggers, value);
		}
		private ObservableCollection<JitDebugger> _debuggers = new();

		public void test() {
			var ico = Icon.ExtractAssociatedIcon("test");

		}

	}
}
