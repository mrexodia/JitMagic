using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PInvoke = Windows.Win32.PInvoke;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using HANDLE = Windows.Win32.Foundation.HANDLE;
#if ! WPF
using System.Windows.Forms;
#endif

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
			var us = $@"""{Assembly.GetExecutingAssembly().Location}"" {OurAeDebugArgs}";
			foreach (var spot in spots) {
				var isVSEntry = spot.EndsWith("JIT");
				var debugVal = isVSEntry ? "Native Debugger" : "Debugger";
				var bkVal = "DebuggerBackup";

				using var sub = Registry.LocalMachine.OpenSubKey(spot, mode != APP_ACTION.RegCheck);

				var curBk = sub.GetValue(bkVal) as string;
				var cur = sub.GetValue(debugVal) as string;
				var isUsNow = mode != APP_ACTION.Unregister ? cur.Equals(us, StringComparison.CurrentCultureIgnoreCase) : cur.StartsWith("\"" + Assembly.GetExecutingAssembly().Location, StringComparison.CurrentCultureIgnoreCase); //for unregistering we dont need exact match just to make sure its us

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
		public void StartDebugger(JitDebugger jitDebugger, int targetPid, String JitDebugStructPtrAddy) {

			var sec = new Windows.Win32.Security.SECURITY_ATTRIBUTES { bInheritHandle = true };
			sec.nLength = (uint)Marshal.SizeOf(sec);

			debugSignalEventForChild = _event != IntPtr.Zero ? PInvoke.CreateEvent(sec, true, false, null) : default;
			var debuggerArgTemplate = jitDebugger.Arguments;
			debuggerArgTemplate = debuggerArgTemplate.Replace("{pid", "{0").Replace("{debugSignalFd", "{1").Replace("{jitDebugInfoPtr", "{2");
			if (debuggerArgTemplate.Contains("{0}") == false && debuggerArgTemplate.Contains("%ld")) // support standard AeDebug strings but only if they don't have one of the expected existing subs
				debuggerArgTemplate = debuggerArgTemplate.Replace("%ld", "{0}").Replace("%ld", "{1}").Replace("%p", "{2}");

			var args = string.Format(debuggerArgTemplate, targetPid, debugSignalEventForChild?.DangerousGetHandle().ToInt32() ?? 0, JitDebugStructPtrAddy);
			var psi = new ProcessStartInfo {
				UseShellExecute = false,
				FileName = jitDebugger.FileName,
				Arguments = args,
			};
			// Undocumented feature of vsjitdebugger.exe that will halt it until a debugger is attached.
			//psi.EnvironmentVariables.Add("VS_Debugging_PauseOnStartup", "1");
			var p = Process.Start(psi);
			if (_event != IntPtr.Zero) {
				PInvoke.WaitForMultipleObjects([new HANDLE(debugSignalEventForChild.DangerousGetHandle()), new HANDLE(p.Handle)], false, uint.MaxValue);
			}
		}
	}
}
