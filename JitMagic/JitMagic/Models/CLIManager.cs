using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
#if ! WPF
using System.Windows.Forms;
#endif

namespace JitMagic.Models {
	public enum APP_ACTION { None, RegCheck, Register, Unregister, AddDebugger, RemoveDebugger, AEDebug, Screenshot }
	public class CLIManager {



		public APP_ACTION mode;
		private string[] args;
		private int CurArg;
		private string GetNextArg() {
			if (args.Length > CurArg)
				return args[CurArg++];
			return null;
		}
		public int ArgsLeft => args.Length - CurArg;
		public class RequestedTargetProc {
			public int Pid;
			public int EventHandleFD;
			public string JitDebugStructPtrAddy;
			public string ProcPath;
			public Architecture Architecture;
		}
		public RequestedTargetProc target;
		public CLIManager(ConfigManager config, AEDebugManager aeDebug, string[] args) {
			this.args = args;
			var action = GetNextArg();
			if (action != null && action.StartsWith("--") && Enum.TryParse<APP_ACTION>(action.Replace("-", ""), true, out var parsed))
				mode = parsed;

			if (mode == APP_ACTION.None) {
				if (action == "-p") {
					try {
						target = new();
						target.Pid = int.Parse(GetNextArg());
						if (GetNextArg() == "-e"){
							target.EventHandleFD = int.Parse(GetNextArg());
							aeDebug.SetEventFD(new IntPtr(target.EventHandleFD));
						}
						if (GetNextArg() == "-j")
							target.JitDebugStructPtrAddy = GetNextArg();
						var process = Process.GetProcessById(target.Pid);
						target.ProcPath = ProcHelper.GetProcessPath(process);
						if (config.Config.BlacklistedPaths.Any(black => black.Equals(target.ProcPath, StringComparison.CurrentCultureIgnoreCase)))
							Environment.Exit(0);

						target.Architecture = ProcHelper.GetProcessArchitecture(process);
						mode = APP_ACTION.AEDebug;
					} catch (Exception ex) {
						MessageBox.Show("Error retrieving information! " + ex);
					}
				}
			}

			if (mode == APP_ACTION.None && config.Config.PerformRegisteredCheckOnStart && !aeDebug.UpdateRegistration(APP_ACTION.RegCheck)) {
				if (MessageBox.Show("We are not currently the default JIT debugger, should we set ourselves as the automatic debugger?", "Update JIT debugger to us?",
#if WPF
					MessageBoxButton.YesNo
#else
					MessageBoxButtons.YesNo
#endif
					) ==
#if WPF
					MessageBoxResult.Yes
#else
					DialogResult.Yes
#endif
					)
					mode = APP_ACTION.Register;

			}

			switch (this.mode) {
				case APP_ACTION.AddDebugger:
				case APP_ACTION.RemoveDebugger:
					var name = GetNextArg();
					if (String.IsNullOrWhiteSpace(name))
						throw new ArgumentException("To add/remove a debugger the name must be passed for the first arg");
					if (mode == APP_ACTION.RemoveDebugger)
						config.RemoveDebugger(name);
					else {
						if (ArgsLeft < 3)
							throw new Exception($"To add a new debugger the form should be JitMagic.exe --add-debugger \"[DebuggerName]\" \"[DebuggerPath]\" \"[DebuggerArgs]\" [x86|x64|All] [AdditionalDelaySecs(optional)]");

						var path = GetNextArg();
						var callArgs = GetNextArg();
						var architecture = GetNextArg();
						if (!Enum.TryParse<Architecture>(architecture, true, out var arch))
							throw new Exception($"Archicture should be x64, x86, or All you passed: {architecture}");
						var deb = new JitDebugger(name, arch) { FileName = path, Arguments = callArgs };
						if (ArgsLeft > 0 && int.TryParse(GetNextArg(), out var addlDelaySecs))
							deb.AdditionalDelaySecs = addlDelaySecs;
						config.AddDebugger(deb);
					
					}
					break;
				case APP_ACTION.Register:
				case APP_ACTION.Unregister:
					ProcHelper.EnsureAdminOrRestartWith(this.mode == APP_ACTION.Register ? "--register" : "--unregister");
					aeDebug.UpdateRegistration(this.mode);
					break;
				case APP_ACTION.AEDebug:
					if (config.Config.IgnoringUntil > DateTime.Now)
						Environment.Exit(0);
					break;
			}
		}
	}
}
