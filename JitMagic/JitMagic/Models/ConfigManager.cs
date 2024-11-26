using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace JitMagic.Models {
	public class ConfigManager {
		public Config Config;

		public void SaveConfig() {
			try {
				if (File.Exists(ConfigFile))
					File.Copy(ConfigFile, BackupConfigFile, true);
			} catch { }

			FileHelper.ReadWriteFile(ConfigFile, JsonConvert.SerializeObject(Config, Formatting.Indented));
		}

		public string ConfigFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JitMagic.json");
		public string BackupConfigFile => ConfigFile + ".bk";

		public void AddDebugger(JitDebugger debugger) => AddRemoveDebugger(debugger.Name, debugger);
		public void RemoveDebugger(String DebuggerName) => AddRemoveDebugger(DebuggerName);
		private void AddRemoveDebugger(String DebuggerName, JitDebugger NewDebugger = null) {
			var debuggers = Config.JitDebuggers.Where(a => a.Name.Equals(DebuggerName, StringComparison.CurrentCultureIgnoreCase) == false).ToList();
			if (NewDebugger != null)
				debuggers.Insert(0, NewDebugger);
			Config.JitDebuggers = debuggers.ToArray();
			SaveConfig();
		}



		public void ReadConfig() {
			Config = new();
			string json = null;
			var configExists = File.Exists(ConfigFile);
			if (configExists) {
				try {
					json = FileHelper.ReadWriteFile(ConfigFile);
					if (!String.IsNullOrWhiteSpace(json))
						Config = JsonConvert.DeserializeObject<Config>(json);
					else
						configExists = false;
				} catch {
					try {
						if (!String.IsNullOrWhiteSpace(json)) {
							Config.JitDebuggers = JsonConvert.DeserializeObject<JitDebugger[]>(json);
							if (Config.JitDebuggers?.Length > 0 == true)
								SaveConfig();//save in new format
						}
					} catch { }
				}
			}
			if (Config.JitDebuggers?.Length > 0 != true)
				Config.JitDebuggers = DefaultDebuggers;
			Config.BlacklistedPaths ??= new();
			Config.BlacklistedPaths.RemoveAll(String.IsNullOrWhiteSpace);
			if (!configExists)
				SaveConfig();

		}
		private static JitDebugger[] DefaultDebuggers = [
			new JitDebugger("Visual Studio", Architecture.All)
			{
				FileName = @"C:\Windows\System32\vsjitdebugger.exe",
				Arguments = "-p {pid} -e {debugSignalFd} -j 0x{jitDebugInfoPtr}"
			},
			new JitDebugger("x32dbg", Architecture.x86)
			{
				FileName = @"c:\Program Files\x64Dbg\x32\x32dbg.exe",
				Arguments = "-a {pid} -e {debugSignalFd}"
			},
			new JitDebugger("x64dbg", Architecture.x64)
			{
				FileName = @"c:\Program Files\x64Dbg\x64\x64dbg.exe",
				Arguments = "-a {pid} -e {debugSignalFd}"
			},
				new JitDebugger("dnSpy (x64)", Architecture.x64) {
				FileName = @"c:\Program Files\dnSpy\dnSpy.exe",
				Arguments = "--dont-load-files --multiple -p {pid} -e {debugSignalFd} --jdinfo {jitDebugInfoPtr}"
			},
				new JitDebugger("dnSpy (x86)", Architecture.x86) {
				FileName = @"c:\Program Files\dnSpy\x86\dnSpy.exe",
				Arguments = "--dont-load-files --multiple -p {pid} -e {debugSignalFd} --jdinfo {jitDebugInfoPtr}"
			},
				new JitDebugger("WinDbg", Architecture.All)
			{
				FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\WinDbgX.exe"),
				Arguments = "-p {pid} -e {debugSignalFd} -g",
				IconOverridePath = @"C:\Windows\System32\shell32.dll,15" //it has an icon but is not accessible would need to resolve true path to the appdata dir and get the dbghash exec
			},
			new JitDebugger("Old WinDbg (x86)", Architecture.x86)
			{
				FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\windbg.exe",
				Arguments = "-p {pid} -e {debugSignalFd} -g"
			},
			new JitDebugger("Old WinDbg (x64)", Architecture.x64)
			{
				FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\windbg.exe",
				Arguments = "-p {pid} -e {debugSignalFd} -g"
			},
			new JitDebugger("ProcDump MiniPlus", Architecture.All)
			{
				FileName = @"c:\Program Files\Sysinternals\procdump.exe",
				Arguments = "-accepteula -mp -j \"c:/dumps\" {pid} {debugSignalFd} {jitDebugInfoPtr}",
				IconOverridePath = @"C:\Windows\System32\MdRes.exe"
			}
			];
	}
	public class Config {
		public JitDebugger[] JitDebuggers { get; set; }
		public int DefaultIgnoreMinutes { get; set; } = 3;
		public bool PerformRegisteredCheckOnStart { get; set; } = true;
		public DateTime IgnoringUntil { get; set; } = DateTime.FromFileTime(0);
		public int OverrideWidth { get; set; } = 0;
		public int OverrideHeight { get; set; } = 0;
		public List<string> BlacklistedPaths { get; set; } = new();
	}
}
