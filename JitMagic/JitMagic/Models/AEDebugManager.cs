using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using PInvoke = Windows.Win32.PInvoke;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using HANDLE = Windows.Win32.Foundation.HANDLE;

namespace JitMagic.Models {
	public class AEDebugManager {
		public const string OurAeDebugArgs = "-p %ld -e %ld -j %p";
		/// <summary>
		/// returns true if we are the current debugger at the end of the request
		/// </summary>
		/// <param name="unregister"></param>
		/// <param name="onlyCheck"></param>
		/// <returns></returns>
		public bool UpdateRegistration(APP_ACTION mode) {
			var spots = new string[] { @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Debugger\JIT" };
			var us = $@"""{ProcHelper.GetCurrentExecutionPath()}"" {OurAeDebugArgs}";
			var writeMode = mode != APP_ACTION.RegCheck && mode != APP_ACTION.Screenshot;
			foreach (var spot in spots) {
				var isVSEntry = spot.EndsWith("JIT");
				var debugVal = isVSEntry ? "Native Debugger" : "Debugger";
				var bkVal = "DebuggerBackup";


				var sub = Registry.LocalMachine.OpenSubKey(spot, writeMode);
				try {
					if (sub == null)
						if (writeMode)
							sub = Registry.LocalMachine.CreateSubKey(spot);
						else
							if (mode == APP_ACTION.RegCheck)
								return false;
							else
								continue;

					var curBk = sub.GetValue(bkVal) as string;
					var cur = sub.GetValue(debugVal) as string;
					var isUsNow = (mode != APP_ACTION.Unregister ? cur?.Equals(us, StringComparison.CurrentCultureIgnoreCase) : cur?.StartsWith("\"" + Assembly.GetExecutingAssembly().Location, StringComparison.CurrentCultureIgnoreCase)) == true; //for unregistering we dont need exact match just to make sure its us

					if (isUsNow ? mode != APP_ACTION.Unregister : mode == APP_ACTION.Unregister)
						continue;

					if (mode == APP_ACTION.RegCheck)
						return false;

					if (mode == APP_ACTION.Register) {
						if (curBk != us && !string.IsNullOrWhiteSpace(cur))
							sub.SetValue(bkVal, cur);

						sub.SetValue(debugVal, us);
						if (!isVSEntry)
							sub.SetValue("Auto", 1);
					} else { //unregister
						if (string.IsNullOrWhiteSpace(curBk)) {
							sub.DeleteValue(debugVal);
							if (!isVSEntry)
								sub.SetValue("Auto", 0);
						} else {
							sub.SetValue(debugVal, curBk);
							sub.DeleteValue(bkVal);
						}
					}
				} finally {
					sub?.Dispose();
				}
			}
			if (mode == APP_ACTION.Register || mode == APP_ACTION.Unregister)
				MessageBox.Show(mode == APP_ACTION.Unregister ? "Removed Us" : "Registered");
			return true;
		}

		public void SignalResume() {
			if (_event != IntPtr.Zero) {
				PInvoke.SetEvent(new HANDLE(_event));
				PInvoke.CloseHandle(new HANDLE(_event));
			}
			_event = IntPtr.Zero;
		}
		IntPtr _event;
		public void SetEventFD(IntPtr fd) => _event = fd;
		private SafeFileHandle debugSignalEventForChild;
		public void StartDebugger(JitDebugger jitDebugger, int targetPid, String JitDebugStructPtrAddy, String CaptureDebuggerOutputTo = null) {

			var sec = new Windows.Win32.Security.SECURITY_ATTRIBUTES { bInheritHandle = true };
			sec.nLength = (uint)Marshal.SizeOf(sec);

			debugSignalEventForChild = _event != IntPtr.Zero ? PInvoke.CreateEvent(sec, true, false, null) : default;
			var debuggerArgTemplate = jitDebugger.Arguments;
			debuggerArgTemplate = debuggerArgTemplate.Replace("{pid", "{0").Replace("{debugSignalFd", "{1").Replace("{jitDebugInfoPtr", "{2");
			if (debuggerArgTemplate.Contains("{0}") == false && debuggerArgTemplate.Contains("%ld")) { // support standard AeDebug strings but only if they don't have one of the expected existing subs
				// One occurrence at a time, the standard order is pid then event handle.  A plain Replace would turn every %ld into the pid.
				var ReplaceFirst = (string haystack, string needle, string with) => {
					var at = haystack.IndexOf(needle, StringComparison.Ordinal);
					return at < 0 ? haystack : haystack.Remove(at, needle.Length).Insert(at, with);
				};
				debuggerArgTemplate = ReplaceFirst(debuggerArgTemplate, "%ld", "{0}");
				debuggerArgTemplate = ReplaceFirst(debuggerArgTemplate, "%ld", "{1}");
				debuggerArgTemplate = ReplaceFirst(debuggerArgTemplate, "%p", "{2}");
			}

			var args = string.Format(debuggerArgTemplate, targetPid, debugSignalEventForChild?.DangerousGetHandle().ToInt32() ?? 0, JitDebugStructPtrAddy);
			var CaptureProcOutput = !String.IsNullOrWhiteSpace(CaptureDebuggerOutputTo);
			var psi = new ProcessStartInfo {
				UseShellExecute = false,
				FileName = jitDebugger.FileName,
				Arguments = args,
			};
			StringBuilder sb = null;

			if (CaptureProcOutput) {
				sb = new();
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;
				psi.StandardOutputEncoding = Encoding.UTF8;
				psi.StandardErrorEncoding = Encoding.UTF8;
			}
			// Undocumented feature of vsjitdebugger.exe that will halt it until a debugger is attached.
			//psi.EnvironmentVariables.Add("VS_Debugging_PauseOnStartup", "1");
			var p = Process.Start(psi);
			if (CaptureProcOutput) {
				p.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
				p.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);
				p.BeginOutputReadLine();
				p.BeginErrorReadLine();

			}
			if (_event != IntPtr.Zero) {
				PInvoke.WaitForMultipleObjects([new HANDLE(debugSignalEventForChild.DangerousGetHandle()), new HANDLE(p.Handle)], false, uint.MaxValue);
			}
			if (CaptureProcOutput) {
				p.WaitForExit();
				System.IO.File.WriteAllText(CaptureDebuggerOutputTo, sb.ToString());
			}

		}

		internal void SilentExit(Process proc, bool dontKillFirst = false) {
			//if we dont kill and wait for it to exit likely the debugger will relaunch
			if (!dontKillFirst && proc != null && proc.Id != 0) {
				if (proc?.HasExited == false) {
					try {

						proc.Kill();
						proc.WaitForExit(10000);
					} catch { }
					System.Threading.Thread.Yield();
					System.Threading.Thread.Sleep(1000);
				}
			}
			Environment.Exit(0);
		}
	}
}
