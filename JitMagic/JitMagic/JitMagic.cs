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
				Arguments = "-p {0} -e {1} -j 0x{2}"
			},
			 new JitDebugger("dnSpy (x64)", Architecture.x64) {
				FileName = @"c:\Program Files\dnSpy\dnSpy.exe",
				Arguments = "--dont-load-files --multiple -p {0} -e {1} --jdinfo {2}"
			},
			 new JitDebugger("dnSpy (x86)", Architecture.x86) {
				FileName = @"c:\Program Files\dnSpy\x86\dnSpy.exe",
				Arguments = "--dont-load-files --multiple -p {0} -e {1} --jdinfo {2}"
			},
			 new JitDebugger("WinDbg", Architecture.All)
			{
				FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\WinDbgX.exe"),
				Arguments = "-p {0} -e {1} -g"
			},
			//"C:\Users\novanix\AppData\Local\Microsoft\WindowsApps\WinDbgX.exe" -p %ld -e %ld -g
			new JitDebugger("Old WinDbg (x86)", Architecture.x86)
			{
				FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\windbg.exe",
				Arguments = "-p {0} -e {1} -g"
			},
			new JitDebugger("Old WinDbg (x64)", Architecture.x64)
			{
				FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\windbg.exe",
				Arguments = "-p {0} -e {1} -g"
			},
			new JitDebugger("ProcDump MiniPlus", Architecture.All)
			{
				FileName = @"c:\Program Files\Sysinternals\procdump.exe",
				Arguments = "-accepteula -mp -j \"c:/dumps\" {0} {1} {2}"
			},
			new JitDebugger("x32dbg", Architecture.x86)
			{
				FileName = @"c:\Program Files\x64Dbg\x32\x32dbg.exe",
				Arguments = "-a {0} -e {1}"
			},
			new JitDebugger("x64dbg", Architecture.x64)
			{
				FileName = @"c:\Program Files\x64Dbg\x64\x64dbg.exe",
				Arguments = "-a {0} -e {1}"
			},
		};
			public int DefaultIgnoreMinutes { get; set; } = 3;
			public bool PerformRegisteredCheckOnStart { get; set; } = true;
			public DateTime IgnoringUntil { get; set; } = DateTime.FromFileTime(0);
			public int OverrideWidth { get; set; } = 0;
			public int OverrideHeight { get; set; } = 0;
		}



		private enum UpdateRegMode { None, Check, Register, Unregister }

		private Config config = new();
		private void SaveConfig() {
			ReadWriteConfigFile(ConfigFile,JsonConvert.SerializeObject(config, Formatting.Indented));
		}
		public string ConfigFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JitMagic.json");
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
		private void DoAction(UpdateRegMode mode) {
			EnsureAdmin(mode);
			UpdateRegistration(mode);
			MessageBox.Show(mode == UpdateRegMode.Unregister ? "Removed Us" : "Registered");
		}
		private void EnsureAdmin(UpdateRegMode mode) {
			if (IsUserAdministrator())
				return;
			Process.Start(new ProcessStartInfo {
				FileName = Assembly.GetExecutingAssembly().Location,
				UseShellExecute = true,
				Verb = "runas",
				Arguments = mode == UpdateRegMode.Register ? "--register" : "--unregister",
			});
			Environment.Exit(0);
		}

		public JitMagic(string[] Args) {


			if (!File.Exists(ConfigFile))
				SaveConfig();
			string json=null;
			try {
				json = ReadWriteConfigFile(ConfigFile);
				config = JsonConvert.DeserializeObject<Config>(json);
			} catch {}
			try {
				if (!String.IsNullOrWhiteSpace(json))
					config.JitDebuggers = JsonConvert.DeserializeObject<JitDebugger[]>(json);
				SaveConfig();//save in new format
			} catch {}
			if (config.IgnoringUntil > DateTime.Now)
				Environment.Exit(0);


			var regAction = UpdateRegMode.None;
			if (config.PerformRegisteredCheckOnStart && Args.Length != 1 && !UpdateRegistration(UpdateRegMode.Check)) {
				if (MessageBox.Show("We are not currently the default JIT debugger do you want to make it us?", "Update JIT debugger to us?", MessageBoxButtons.YesNo) == DialogResult.Yes)
					regAction = UpdateRegMode.Register;
			} else if (Args.Length == 1) {
				if (Args[0] == "--register")
					regAction = UpdateRegMode.Register;
				else if (Args[0] == "--unregister")
					regAction = UpdateRegMode.Unregister;
			}
			if (regAction != UpdateRegMode.None) {
				DoAction(regAction);
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
					var architecture = GetProcessArchitecture(process);
					labelProcessInformation.Text = $"{Path.GetFileName(GetProcessPath(_pid))} ({architecture})";
					PopulateJitDebuggers(architecture);

				} catch (Exception ex) {
					MessageBox.Show("Error retrieving information! " + ex);
				}
				HaveInvokeDetails = true;
			} else {
				PopulateJitDebuggers(Architecture.All);
			}
			listViewDebuggers.ItemActivate += OnItemClicked;
		}
		private bool HaveInvokeDetails;

		private void OnItemClicked(object sender, EventArgs e) {

			if (HaveInvokeDetails)
				Hide();
			var jitDebugger = listViewDebuggers.SelectedItems[0].Tag as JitDebugger;

			var sec = new Windows.Win32.Security.SECURITY_ATTRIBUTES { bInheritHandle = true };
			sec.nLength = (uint)Marshal.SizeOf(sec);

			debugSignalEventForChild = HaveInvokeDetails ? PInvoke.CreateEvent(sec, true, false, null) : default;

			var args = string.Format(jitDebugger.Arguments, _pid, debugSignalEventForChild.DangerousGetHandle().ToInt32(), JitDebugStructPtrAddy);
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
				using var handle = PInvoke.CreateFile(ConfigFile, default, default, null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT, default);
				if (handle.IsInvalid)
					throw new Win32Exception();

				Span<sbyte> buffer = new sbyte[PInvoke.MAXIMUM_REPARSE_DATA_BUFFER_SIZE + Marshal.SizeOf<Windows.Wdk.Storage.FileSystem.REPARSE_DATA_BUFFER>()];
				buffer.Clear();

				fixed (sbyte* ptr = buffer) {
					ref var itm = ref *(Windows.Wdk.Storage.FileSystem.REPARSE_DATA_BUFFER*)ptr;


					uint bytes;
					if (!PInvoke.DeviceIoControl(handle, PInvoke.FSCTL_GET_REPARSE_POINT, null, 0, ptr, (uint)buffer.Length, &bytes, null))
						throw new Win32Exception();

					if (itm.ReparseTag != PInvoke.IO_REPARSE_TAG_SYMLINK)
						throw new Exception("File was not a symlink");
					ref var symReparse = ref itm.Anonymous.SymbolicLinkReparseBuffer;
					var path = symReparse.PathBuffer.AsSpan((int)bytes / sizeof(char)).Slice(symReparse.SubstituteNameOffset / sizeof(char)); //it cant be any longer than the total bytes returned
					var str = CStrToString(path);
					if (str.Length < 4 || !str.StartsWith(@"\??\"))
						throw new Exception("Invalid symlink read");
					var fileName = str.Substring(4);
					if (!File.Exists(fileName))
						throw new FileNotFoundException($"Symlink target does not exist: {fileName}");

					return action(fileName);
				}

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
			listViewDebuggers.LargeImageList = new ImageList();
			listViewDebuggers.LargeImageList.ImageSize = new Size(80, 60);
			for (var i = 0; i < config.JitDebuggers.Length; i++) {
				var jitDebugger = config.JitDebuggers[i];
				if (!File.Exists(jitDebugger.FileName))
					continue;

				var icon = Icon.ExtractAssociatedIcon(jitDebugger.FileName);
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
			Environment.Exit(0);
		}

		private void btnRemoveUs_Click(object sender, EventArgs e) => DoAction(UpdateRegMode.Unregister);


		/// <summary>
		/// returns true if we are the current debugger at the end of the request
		/// </summary>
		/// <param name="unregister"></param>
		/// <param name="onlyCheck"></param>
		/// <returns></returns>
		private bool UpdateRegistration(UpdateRegMode mode) {
			var spots = new string[] { @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\AeDebug", @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Debugger\JIT" };
			var us = $@"""{Assembly.GetExecutingAssembly().Location}"" -p %ld -e %ld -j %p";
			foreach (var spot in spots) {
				var isVSEntry = spot.EndsWith("JIT");
				var debugVal = isVSEntry ? "Native Debugger" : "Debugger";
				var bkVal = "DebuggerBackup";

				using var sub = Registry.LocalMachine.OpenSubKey(spot, mode != UpdateRegMode.Check);

				var curBk = sub.GetValue(bkVal) as string;
				var cur = sub.GetValue(debugVal) as string;
				var isUsNow = mode != UpdateRegMode.Unregister ? cur.Equals(us, StringComparison.CurrentCultureIgnoreCase) : cur.StartsWith("\"" + Assembly.GetExecutingAssembly().Location, StringComparison.CurrentCultureIgnoreCase); //for unregistering we dont need exact match just to make sure its us

				if (isUsNow ? mode != UpdateRegMode.Unregister : mode == UpdateRegMode.Unregister)
					continue;

				if (mode == UpdateRegMode.Check)
					return false;

				if (mode == UpdateRegMode.Register) {
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
	}
}
