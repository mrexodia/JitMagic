using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
			public Process Process;
			public bool HasExited => Process?.HasExited ?? true;
			public int EventHandleFD;
			public string JitDebugStructPtrAddy;
			public string ProcPath;
			public Architecture Architecture = Architecture.Invalid;
			public enum TARGET_STATUS { NotFound, Waiting, Exited }
			public TARGET_STATUS Status => Process == null ? TARGET_STATUS.NotFound : HasExited ? TARGET_STATUS.Exited : TARGET_STATUS.Waiting;
		}
		public RequestedTargetProc target;
		public string ErrorMsg;
		public CLIManager(ConfigManager config, AEDebugManager aeDebug, string[] args) {
			this.args = args;
			var action = GetNextArg();
			if (action != null && action.StartsWith("--") && Enum.TryParse<APP_ACTION>(action.Replace("-", ""), true, out var parsed))
				mode = parsed;

			if (mode == APP_ACTION.AEDebug) {
				MessageBox.Show("AEDebug mode should not be passed with --AEdebug but rather by passing -p with the pid to debug", "Invalid Arg", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(1);
			}

			if (mode == APP_ACTION.None) {


				if (action == "-p") {
					mode = APP_ACTION.AEDebug;
					if (config.Config.IgnoringUntil > DateTime.Now)
						aeDebug.SilentExit(target.Process, config.Config.DontKillTargetProcessOnNonDebugExit);
					try {
						target = new();
						target.Pid = int.Parse(GetNextArg());
						if (GetNextArg() == "-e") {
							target.EventHandleFD = int.Parse(GetNextArg());
							aeDebug.SetEventFD(new IntPtr(target.EventHandleFD));
						}
						if (GetNextArg() == "-j")
							target.JitDebugStructPtrAddy = GetNextArg();
						try {
							target.Process = Process.GetProcessById(target.Pid);
						} catch (ArgumentException) { } //processes exited (or we got an invalid pid arg)


						if (target.Status == RequestedTargetProc.TARGET_STATUS.Waiting) {
							target.ProcPath = ProcHelper.GetProcessPath(target.Process);
							if (String.IsNullOrWhiteSpace(target.ProcPath) && target.Status == RequestedTargetProc.TARGET_STATUS.Waiting)
								target.ProcPath = target.Process.MainModule?.FileName;//try this if the WMI query failed, may still fail if process is protected but worth a shot
							if (!String.IsNullOrWhiteSpace(config.Config.IgnoreProcessesWithSideBySideFileExtension)) {
								var noJitTestFile = Path.Combine(Path.GetDirectoryName(target.ProcPath), Path.GetFileNameWithoutExtension(target.ProcPath) + config.Config.IgnoreProcessesWithSideBySideFileExtension);
								if (File.Exists(noJitTestFile)) {
									aeDebug.SilentExit(target.Process, config.Config.DontKillTargetProcessOnNonDebugExit || config.Config.DontKillBlacklistedProcesses);//this will hard exit us
									return;
								}
							}
							if (config.Config.BlacklistedPaths.Contains(target.ProcPath, StringComparer.CurrentCultureIgnoreCase))
								aeDebug.SilentExit(target.Process, config.Config.DontKillBlacklistedProcesses || config.Config.DontKillBlacklistedProcesses);//this will hard exit us
							target.Architecture = ProcHelper.GetProcessArchitecture(target.Process);
						}


					} catch (Exception ex) {
						ErrorMsg = ex.Message;
					}
				}
			}

			if (mode == APP_ACTION.None && config.Config.PerformRegisteredCheckOnStart && !aeDebug.UpdateRegistration(APP_ACTION.RegCheck)) {
				if (MessageBox.Show("We are not currently the default JIT debugger, should we set ourselves as the automatic debugger?", "Update JIT debugger to us?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
			}
		}
	}
}
