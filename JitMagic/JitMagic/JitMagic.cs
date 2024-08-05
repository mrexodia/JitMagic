using System;
using Windows.Win32.Storage.FileSystem;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Win32;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.Win32.SafeHandles;
using Windows.Wdk.Storage.FileSystem;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace JitMagic {
	public partial class JitMagic : Form {
		class JitDebugger {
			public JitDebugger(string name, Architecture architecture) {
				Name = name;
				Architecture = architecture;
			}

			public string Name { get; }
			public Architecture Architecture { get; }
			public string FileName { get; set; }
			public string Arguments { get; set; }
			public string IconOverridePath { get; set; }
			public int AdditionalDelaySecs { get; set; } = 0; // Additional time after it would normally exit where it exits.  Good for  misbehaving / non-signalling debuggers.
		}

		int _pid;
		String JitDebugStructPtrAddy;
		IntPtr _event;
		class Config {
			public JitDebugger[] JitDebuggers { get; set; } = new[] {
				new JitDebugger("Visual Studio", Architecture.All)
				{
					FileName = @"C:\Windows\System32\vsjitdebugger.exe",
					Arguments = "-p {pid} -e {debugSignalFd} -j 0x{jitDebugInfoPtr}"
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
					Arguments = "-p {pid} -e {debugSignalFd} -g"
				},
				//"C:\Users\novanix\AppData\Local\Microsoft\WindowsApps\WinDbgX.exe" -p %ld -e %ld -g
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
		};
			public int DefaultIgnoreMinutes { get; set; } = 3;
			public bool PerformRegisteredCheckOnStart { get; set; } = true;
			public DateTime IgnoringUntil { get; set; } = DateTime.FromFileTime(0);
			public int OverrideWidth { get; set; } = 0;
			public int OverrideHeight { get; set; } = 0;
			public List<string> BlacklistedPaths { get; set; } = new();
		}



		private enum APP_ACTION { None, RegCheck, Register, Unregister, AddDebugger, RemoveDebugger }

		private Config config = new();
		private void SaveConfig() {
			try {
				if (File.Exists(ConfigFile))
					File.Copy(ConfigFile, BackupConfigFile, true);
			} catch { }
			ReadWriteConfigFile(ConfigFile, JsonConvert.SerializeObject(config, Formatting.Indented));
		}

		public string ConfigFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JitMagic.json");
		public string BackupConfigFile => ConfigFile + ".bk";
		public static bool IsUserAdministrator() {
			//bool value to hold our return value
			bool isAdmin;
			try {
				//get the currently logged in user
				WindowsIdentity user = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal(user);
				isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
			} catch (UnauthorizedAccessException) {
				isAdmin = false;
			} catch (Exception) {
				isAdmin = false;
			}
			return isAdmin;
		}
		private void DoAction(APP_ACTION mode, string[] args) {
			switch (mode) {
				case APP_ACTION.AddDebugger:
				case APP_ACTION.RemoveDebugger:
					AddRemoveDebugger(mode, args);
					break;
				case APP_ACTION.Register:
				case APP_ACTION.Unregister:
					EnsureAdmin(mode);
					UpdateRegistration(mode);
					MessageBox.Show(mode == APP_ACTION.Unregister ? "Removed Us" : "Registered");
					break;
			}
		}

		private void AddRemoveDebugger(APP_ACTION mode, string[] args) {
			try {
				var pos = 1;

				var name = (args.Length > pos) ? args[pos++] : throw new ArgumentException("To add/remove a debugger the name must be passed for the first arg");
				config.JitDebuggers = config.JitDebuggers.Where(a => a.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) == false).ToArray();
				if (mode == APP_ACTION.RemoveDebugger) {
					SaveConfig();
					MessageBox.Show($"Removed debuggers with name: {name}");
					Close();
				} else {
					if (pos + 3 > args.Length)
						throw new Exception($"To add a new debugger the form should be JitMagic.exe --add-debugger \"[DebuggerName]\" \"[DebuggerPath]\" \"[DebuggerArgs]\" [x86|x64|All] [AdditionalDelaySecs(optional)]");
					var path = args[pos++];
					var callArgs = args[pos++];
					var architecture = args[pos++];
					if (!Enum.TryParse<Architecture>(architecture, true, out var arch))
						throw new Exception($"Archicture should be x64, x86, or All you passed: {architecture}");

					var deb = new JitDebugger(name, arch) { FileName = path, Arguments = callArgs };
					if (args.Length > pos && int.TryParse(args[pos++], out var addlDelaySecs))
						deb.AdditionalDelaySecs = addlDelaySecs;
					config.JitDebuggers = new[] { deb }.Union(config.JitDebuggers).ToArray();
					SaveConfig();
					MessageBox.Show($"Added debugger: {name}");
				}
			} catch (Exception ex) {

			}
		}

		private void EnsureAdmin(APP_ACTION mode) {
			if (IsUserAdministrator())
				return;
			Process.Start(new ProcessStartInfo {
				FileName = Assembly.GetExecutingAssembly().Location,
				UseShellExecute = true,
				Verb = "runas",
				Arguments = mode == APP_ACTION.Register ? "--register" : "--unregister",
			});
			Environment.Exit(0);
		}

		public JitMagic(string[] Args) {
			if (!File.Exists(ConfigFile))
				SaveConfig();
			var action = Args.Length > 0 ? Args[0] : null;
			var appAction = APP_ACTION.None;
			if (action != null && action.StartsWith("--") && Enum.TryParse<APP_ACTION>(action.Replace("-", ""), true, out var parsed))
				appAction = parsed;

			string json = null;
			try {
				json = ReadWriteConfigFile(ConfigFile);
				config = JsonConvert.DeserializeObject<Config>(json);
				config.BlacklistedPaths ??= new();
				config.BlacklistedPaths.RemoveAll(s => s == null);
			} catch {
				try {
					if (!String.IsNullOrWhiteSpace(json))
						config.JitDebuggers = JsonConvert.DeserializeObject<JitDebugger[]>(json);
					SaveConfig();//save in new format
				} catch { }
			}
			if (config.IgnoringUntil > DateTime.Now && Args.Length > 1)
				Environment.Exit(0);



			if (appAction == APP_ACTION.None && config.PerformRegisteredCheckOnStart && Args.Length != 1 && !UpdateRegistration(APP_ACTION.RegCheck)) {
				if (MessageBox.Show("We are not currently the default JIT debugger do you want to make it us?", "Update JIT debugger to us?", MessageBoxButtons.YesNo) == DialogResult.Yes)
					appAction = APP_ACTION.Register;
			}
			if (appAction != APP_ACTION.None) {
				DoAction(appAction, Args);
				Environment.Exit(0);
			}

			InitializeComponent();
			if (Debugger.IsAttached)
				TopMost = false;
			KeyPreview = true;
			AcceptButton = btnAttach;
			txtIgnore.Value = config.DefaultIgnoreMinutes;
			Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

			if (config.OverrideWidth > 100)
				Width = config.OverrideWidth;
			if (config.OverrideHeight > 100)
				Height = config.OverrideHeight;

			HaveInvokeDetails = false;
			if (Args.Length >= 4 && Args[0] == "-p" && Args[2] == "-e") {
				if (Args.Length >= 6 && Args[4] == "-j")
					JitDebugStructPtrAddy = Args[5];
				_pid = int.Parse(Args[1]);
				_event = new IntPtr(int.Parse(Args[3]));
				Text = $"JitMagic - PID: {_pid}";
				try {

					var process = Process.GetProcessById(_pid);
					processPath = GetProcessPath(_pid);
					if (config.BlacklistedPaths.Any(black => black.Equals(processPath, StringComparison.CurrentCultureIgnoreCase)))
						Environment.Exit(0);

					var architecture = GetProcessArchitecture(process);
					labelProcessInformation.Text = $"{Path.GetFileName(processPath)} ({architecture})";
					PopulateJitDebuggers(architecture);

				} catch (Exception ex) {
					MessageBox.Show("Error retrieving information! " + ex);
				}
				HaveInvokeDetails = true;
			} else
				PopulateJitDebuggers(Architecture.All);

			listViewDebuggers.ItemActivate += OnItemClicked;
		}
		private string processPath;
		private bool HaveInvokeDetails;

		private void OnItemClicked(object sender, EventArgs e) {

			if (HaveInvokeDetails)
				Hide();
			var jitDebugger = listViewDebuggers.SelectedItems[0].Tag as JitDebugger;

			var sec = new Windows.Win32.Security.SECURITY_ATTRIBUTES { bInheritHandle = true };
			sec.nLength = (uint)Marshal.SizeOf(sec);

			debugSignalEventForChild = HaveInvokeDetails ? PInvoke.CreateEvent(sec, true, false, null) : default;
			var debuggerArgTemplate = jitDebugger.Arguments;
			debuggerArgTemplate = debuggerArgTemplate.Replace("{pid", "{0").Replace("{debugSignalFd", "{1").Replace("{jitDebugInfoPtr", "{2");
			if (debuggerArgTemplate.Contains("{0}") == false && debuggerArgTemplate.Contains("%ld")) // support standard AeDebug strings but only if they don't have one of the expected existing subs
				debuggerArgTemplate = debuggerArgTemplate.Replace("%ld", "{0}").Replace("%ld", "{1}").Replace("%p", "{2}");

			var args = string.Format(debuggerArgTemplate, _pid, debugSignalEventForChild.DangerousGetHandle().ToInt32(), JitDebugStructPtrAddy);
			var psi = new ProcessStartInfo {
				UseShellExecute = false,
				FileName = jitDebugger.FileName,
				Arguments = args,
			};
			// Undocumented feature of vsjitdebugger.exe that will halt it until a debugger is attached.
			//psi.EnvironmentVariables.Add("VS_Debugging_PauseOnStartup", "1");
			var p = Process.Start(psi);
			if (HaveInvokeDetails) {
				PInvoke.WaitForMultipleObjects(new HANDLE[] { new HANDLE(debugSignalEventForChild.DangerousGetHandle()), new HANDLE(p.Handle) }, false, uint.MaxValue);
				DelayClose(jitDebugger.AdditionalDelaySecs);
			}
		}
		private SafeFileHandle debugSignalEventForChild;
		private async void DelayClose(int extraDelaySecs) {
			if (extraDelaySecs > 0)
				await Task.Delay(TimeSpan.FromSeconds(extraDelaySecs));
			Close();
		}
		public static string CStrToString(Span<char> str) {
			var pos = str.IndexOf((char)0);
			if (pos == -1)
				throw new Exception("invalid string");
			return str.Slice(0, pos).ToString();
		}

		/// <summary>
		/// Tries to read or write the file directly, if it fails due to control flow guard try to work around that.  if toWrite is null it reads the file and returns the data otherwise it writes toWrite to the file.
		/// </summary>
		/// <param name="ConfigFile"></param>
		/// <param name="toWrite"></param>
		/// <returns></returns>
		/// <exception cref="Win32Exception"></exception>
		/// <exception cref="Exception"></exception>
		/// <exception cref="FileNotFoundException"></exception>
		private unsafe string ReadWriteConfigFile(string ConfigFile, string toWrite = null) {
			Func<string, string> action = (string fileName) => {
				if (toWrite == null)
					return File.ReadAllText(fileName);
				File.WriteAllText(fileName, toWrite);
				return null;
			};

			try {
				return action(ConfigFile);
			} catch (IOException) { //This is to try and work around an issue where for actual exceptions (vs debugger.breaks) RedirectionGuard is enabled and it prevents us from reading the config if it is a symlink.  This is the only way to read it that I have found.
				var fileName = GetSymLink(ConfigFile, RELATIVE_LINK_MODE.Resolve);
				if (File.Exists(fileName))
					return action(fileName);
				throw new Exception("Config file not found");

			}
		}
		public enum RELATIVE_LINK_MODE { Disallow, Preserve, Resolve }
		private const string WIN32_NAMESPACE_PREFIX = @"\??\";
		private const string UNC_PREFIX = @"UNC\";
		/// <summary>
		/// Manually resolve a file to its target, needed, for example, if GetFinalPathNameByHandle cannot be called due to RedirectionGuard preventing it in certain security contexts
		/// </summary>
		/// <param name="file">file to resolve to path</param>
		/// <param name="rel_mode">What to do with relative sym links (ie ../test.txt)</param>
		/// <param name="AllowVolumeMountpoints">Allow junctions that resolve to \??\Volume{«guid»}\....</param>
		/// <returns></returns>
		/// <exception cref="Win32Exception"></exception>
		/// <exception cref="IOException"></exception>
		public static unsafe string GetSymLink(string file, RELATIVE_LINK_MODE rel_mode = RELATIVE_LINK_MODE.Disallow, bool AllowVolumeMountpoints = false) {
			using var handle = PInvoke.CreateFile(file, default, default, null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT, default);
			if (handle.IsInvalid)
				throw new Win32Exception();

			Span<sbyte> buffer = new sbyte[PInvoke.MAXIMUM_REPARSE_DATA_BUFFER_SIZE];

			fixed (sbyte* ptr = buffer) {
				ref var itm = ref *(Windows.Wdk.Storage.FileSystem.REPARSE_DATA_BUFFER*)ptr;


				uint bytes;
				if (!PInvoke.DeviceIoControl(handle, PInvoke.FSCTL_GET_REPARSE_POINT, null, 0, ptr, (uint)buffer.Length, &bytes, null))
					throw new Win32Exception();
				Span<char> returnPath = null;

				static Span<char> ParsePathBuffer(ref VariableLengthInlineArray<char> buffer, int nameOffsetInBytes, int lengthInBytes, out bool WasPrefixed) {
					var ret = buffer.AsSpan((nameOffsetInBytes + lengthInBytes) / sizeof(char)).Slice(nameOffsetInBytes / sizeof(char));
					WasPrefixed = ret.Length >= WIN32_NAMESPACE_PREFIX.Length && ret.StartsWith(WIN32_NAMESPACE_PREFIX.ToArray());
					if (WasPrefixed)
						ret = ret.Slice(WIN32_NAMESPACE_PREFIX.Length);
					return ret;
				}

				if (itm.ReparseTag == PInvoke.IO_REPARSE_TAG_SYMLINK) {

					ref var reparse = ref itm.Anonymous.SymbolicLinkReparseBuffer;
					returnPath = ParsePathBuffer(ref reparse.PathBuffer, reparse.SubstituteNameOffset, reparse.SubstituteNameLength, out var wasWin32NamespacePrefixed);

					var shouldBeRelativeLink = (reparse.Flags & Windows.Wdk.PInvoke.SYMLINK_FLAG_RELATIVE) != 0;
					if (returnPath.Length == 0 || (!wasWin32NamespacePrefixed && !shouldBeRelativeLink))
						throw new IOException("Invalid symlink read");
					else if (shouldBeRelativeLink) { //this should be a relative link as was not prefixed
						if (rel_mode == RELATIVE_LINK_MODE.Disallow)
							throw new IOException($"Relative symlink found of: {returnPath.ToString()} but relative links disabled");
						else if (rel_mode == RELATIVE_LINK_MODE.Resolve)
							return Path.Combine(new FileInfo(file).DirectoryName, returnPath.ToString());
						//netcore only: return Path.GetFullPath(returnPath.ToString(), new FileInfo(file).DirectoryName);
					}
				} else if (itm.ReparseTag == PInvoke.IO_REPARSE_TAG_MOUNT_POINT) {
					ref var reparse = ref itm.Anonymous.MountPointReparseBuffer;
					returnPath = ParsePathBuffer(ref reparse.PathBuffer, reparse.SubstituteNameOffset, reparse.SubstituteNameLength, out var wasWin32NamespacePrefixed);
					if (!wasWin32NamespacePrefixed)
						throw new IOException("Invalid junction read");
					if (!AllowVolumeMountpoints && (!IsAsciiLetter(returnPath[0]) || returnPath[1] != ':'))
						throw new IOException("File is a junction to a volume mount point and that is disabled");
				}

				return returnPath.ToString();
			}
		}


		protected override void OnClosing(CancelEventArgs e) {
			if (_event != IntPtr.Zero) {
				PInvoke.SetEvent(new HANDLE(_event));
				PInvoke.CloseHandle(new HANDLE(_event));
			}
			base.OnClosing(e);
		}

		protected override void OnKeyDown(KeyEventArgs e) {
			if (e.KeyCode == Keys.Escape)
				Close();
			base.OnKeyDown(e);
		}

		void PopulateJitDebuggers(Architecture architecture) {
			var iconFromAppRegex = new Regex(@"^(?<path>.+[.](?:exe|dll))(?:[,](?<index>[\-0-9]+))?$", RegexOptions.IgnoreCase);
			listViewDebuggers.LargeImageList = new ImageList();
			listViewDebuggers.LargeImageList.ImageSize = new Size(80, 60);

			var GetIconMethod = (string path, int index) => Icon.ExtractAssociatedIcon(path);//backup method, downside is it can't take an index
			try {
				var mInfo = typeof(Icon).GetMethod(nameof(Icon.ExtractAssociatedIcon), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
				if (mInfo != null) {
					var args = mInfo.GetParameters();
					if (args.Length == 2 && args[0].ParameterType == typeof(string) && args[1].ParameterType == typeof(int))
						GetIconMethod = (string path, int index) => (Icon)mInfo.Invoke(null, [path, index]);
				}
			} catch { }


			for (var i = 0; i < config.JitDebuggers.Length; i++) {
				var jitDebugger = config.JitDebuggers[i];
				if (!File.Exists(jitDebugger.FileName))
					continue;
				Icon icon = null;
				try {
					var extractPath = jitDebugger.FileName;
					var extractIndex = 0;
					if (!String.IsNullOrWhiteSpace(jitDebugger.IconOverridePath)) {
						var extractMatch = iconFromAppRegex.Match(jitDebugger.IconOverridePath);
						if (extractMatch.Success) {
							extractPath = extractMatch.Groups["path"].Value;
							if (extractMatch.Groups["index"].Success)
								extractIndex = int.Parse(extractMatch.Groups["index"].Value);
						} else
							icon = new Icon(jitDebugger.IconOverridePath);

					}
					if (icon == null)
						icon = GetIconMethod(extractPath, extractIndex);
				} catch { }
				if (icon == null)
					icon = Icon;
				listViewDebuggers.LargeImageList.Images.Add(icon.ToBitmap());

				if (architecture == Architecture.All || jitDebugger.Architecture == architecture || jitDebugger.Architecture == Architecture.All) {
					listViewDebuggers.Items.Add(new ListViewItem(jitDebugger.Name) {
						ImageIndex = listViewDebuggers.Items.Count,
						Tag = jitDebugger
					});
				}
			}
			if (listViewDebuggers.Items.Count > 0)
				listViewDebuggers.Items[0].Selected = true;
			listViewDebuggers.Focus();
		}

		[JsonConverter(typeof(StringEnumConverter))]
		enum Architecture {
			x64,
			x86,
			All
		}

		static Architecture GetProcessArchitecture(Process p) {
			if (Environment.Is64BitOperatingSystem) {
				if (PInvoke.IsWow64Process(new SafeProcessHandle(p.Handle, false), out var iswow64)) {
					return iswow64 ? Architecture.x86 : Architecture.x64;
				}
				throw new Exception("IsWow64Process failed");
			} else {
				return Architecture.x86;
			}
		}

		static string GetProcessPath(int processId) {
			string MethodResult = "";
			try {
				string Query = "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;

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


		private void btnAttach_Click(object sender, EventArgs e) {
			if (listViewDebuggers.SelectedItems.Count == 0)
				return;
			var item = listViewDebuggers.SelectedItems[0];
			var onItemActivate = item.ListView.GetType().GetMethod("OnItemActivate", BindingFlags.NonPublic | BindingFlags.Instance);
			item.Selected = true;
			onItemActivate.Invoke(item.ListView, [EventArgs.Empty]);
		}

		private void btnIgnoreAll_Click(object sender, EventArgs e) {
			config.IgnoringUntil = DateTime.Now.AddMinutes((double)txtIgnore.Value);
			SaveConfig();
			Close();
		}

		private void btnRemoveUs_Click(object sender, EventArgs e) => DoAction(APP_ACTION.Unregister, null);


		/// <summary>
		/// returns true if we are the current debugger at the end of the request
		/// </summary>
		/// <param name="unregister"></param>
		/// <param name="onlyCheck"></param>
		/// <returns></returns>
		private bool UpdateRegistration(APP_ACTION mode) {
			var spots = new string[] { @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Debugger\JIT" };
			var us = $@"""{Assembly.GetExecutingAssembly().Location}"" -p %ld -e %ld -j %p";
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
			return true;
		}

		private void btnBlacklistPath_Click(object sender, EventArgs e) {
			var confirm = MessageBox.Show($"Are you sure you want to blacklist the executable path: {processPath} from future debugging? The only way to undo this is to manually edit the JitMagic.json file", $"Confirm Blacklist {Path.GetFileName(processPath)}", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
			if (confirm != DialogResult.Yes)
				return;
			config.BlacklistedPaths.Add(processPath);
			SaveConfig();
			Close();
		}
		public static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
	}

}
