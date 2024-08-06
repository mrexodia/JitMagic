using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace JitMagic.Models {
	static class ProcHelper {
		public static bool IsUserAdministrator() {
			//bool value to hold our return value
			bool isAdmin;
			try {
				//get the currently logged in user
				var user = WindowsIdentity.GetCurrent();
				var principal = new WindowsPrincipal(user);
				isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
			} catch (UnauthorizedAccessException) {
				isAdmin = false;
			} catch (Exception) {
				isAdmin = false;
			}
			return isAdmin;
		}
		public static void LaunchUs(bool asAdmin, String args = null) {
			Process.Start(new ProcessStartInfo {
				FileName = Assembly.GetExecutingAssembly().Location,
				UseShellExecute = true,
				Verb = asAdmin ? "runas" : "",
				Arguments = args,
			});
		}
		public static void EnsureAdminOrRestartWith(String restartWithArg) {
			if (IsUserAdministrator())
				return;
			LaunchUs(true, restartWithArg);
			Environment.Exit(0);
		}
		public static Architecture GetProcessArchitecture(Process p) {
			if (p.Id == 0 || p.HasExited)
				return Architecture.x64;
			if (Environment.Is64BitOperatingSystem) {
				if (Windows.Win32.PInvoke.IsWow64Process(new SafeProcessHandle(p.Handle, false), out var iswow64)) {
					return iswow64 ? Architecture.x86 : Architecture.x64;
				}
				throw new Exception("IsWow64Process failed");
			} else {
				return Architecture.x86;
			}
		}
		public static string GetProcessPath(Process p) {
			if (p.Id == 0 || p.HasExited)
				return null;
			string MethodResult = "";
			try {
				string Query = "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + p.Id;

				using (ManagementObjectSearcher mos = new ManagementObjectSearcher(Query)) {
					using (ManagementObjectCollection moc = mos.Get()) {
						string ExecutablePath = (from mo in moc.Cast<ManagementObject>() select mo["ExecutablePath"]).First().ToString();
						MethodResult = ExecutablePath;
					}
				}
			} catch {
			}
			return MethodResult;
		}
	}
}
