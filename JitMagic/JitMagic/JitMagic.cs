using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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

namespace JitMagic
{
    public partial class JitMagic : Form
    {
        class JitDebugger
        {
            public JitDebugger(string name, Architecture architecture)
            {
                Name = name;
                Architecture = architecture;
            }

            public string Name { get; }
            public Architecture Architecture { get; }
            public string FileName { get; set; }
            public string Arguments { get; set; }
        }

        int _pid;
        IntPtr _event;
        IntPtr _jdinfo; // https://renenyffenegger.ch/notes/Windows/registry/tree/HKEY_LOCAL_MACHINE/Software/Microsoft/Windows-NT/CurrentVersion/AeDebug/index

        JitDebugger[] _jitDebuggers = new[]
        {
            new JitDebugger("Visual Studio", Architecture.x86)
            {
                FileName = @"C:\Windows\SysWOW64\vsjitdebugger.exe",
                Arguments = "-p {0} -e {1} -j 0x{2}"
            },
            new JitDebugger("Visual Studio", Architecture.x64)
            {
                FileName = @"C:\Windows\System32\vsjitdebugger.exe",
                Arguments = "-p {0} -e {1} -j 0x{2}"
            },
            new JitDebugger("WinDbg", Architecture.x86)
            {
                FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\windbg.exe",
                Arguments = "-p {0} -e {1} -g"
            },
            new JitDebugger("WinDbg", Architecture.x64)
            {
                FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\windbg.exe",
                Arguments = "-p {0} -e {1} -g"
            },
            new JitDebugger("x32dbg", Architecture.x86)
            {
                FileName = @"c:\x64dbg\bin\x32\x32dbg.exe",
                Arguments = "-a {0} -e {1}"
            },
            new JitDebugger("x64dbg", Architecture.x64)
            {
                FileName = @"c:\x64dbg\bin\x64\x64dbg.exe",
                Arguments = "-a {0} -e {1}"
            },
        };

        [DllImport("kernel32.dll")]
        static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool lpSystemInfo);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateEvent(ref SECURITY_ATTRIBUTES lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll")]
        static extern Int32 WaitForSingleObject(IntPtr hHandle, Int32 dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern Int32 WaitForMultipleObjects(Int32 Count, ref HANDLES lpHandles, bool bWaitAll, Int32 dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        struct HANDLES
        {
            public IntPtr h1;
            public IntPtr h2;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SECURITY_ATTRIBUTES
        {
            public Int32 length;
            public IntPtr securityDesc;
            public bool inherit;
        }

        public static void CheckRegistry(bool fix = false)
        {
            /*
            vsjitdebugger.exe:
                RegOpenKeyExW(HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\AeDebug)
                RegQueryValueExW(Debugger)
                RegOpenKeyExW(HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug)
                RegQueryValueExW(Debugger)
                should be equal to: HKLM\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Debugger\JIT\Native Debugger
            */

            var sbError = new StringBuilder();
            var assembly = Assembly.GetExecutingAssembly().Location;
            var expectedJit = $"\"{assembly}\" -p %ld -e %ld -j 0x%p";

            using (var reg32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (var jit = reg32.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\Debugger\JIT", fix))
                {
                    var nativeDebugger = jit.GetValue("Native Debugger") as string;
                    if (nativeDebugger != expectedJit)
                    {
                        if (fix)
                            jit.SetValue("Native Debugger", expectedJit);
                        sbError.AppendLine($"- JIT\\Native Debugger is wrong ({nativeDebugger})");
                    }
                }

                using (var aeDebug = reg32.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug", fix))
                {
                    var debugger = aeDebug.GetValue("Debugger", "") as string;
                    if (debugger != expectedJit)
                    {
                        if (fix)
                            aeDebug.SetValue("Debugger", expectedJit);
                        sbError.AppendLine($"- AeDebug\\Debugger (32 bit) is wrong ({debugger})");
                    }
                    var auto = aeDebug.GetValue("Auto", "").ToString();
                    if (auto != "1")
                    {
                        if (fix)
                            aeDebug.SetValue("Auto", "1");
                        sbError.AppendLine($"- AeDebug\\Auto (32 bit) is wrong ({auto.Replace("\0", "\\0")})");
                    }
                }
            }

            using (var reg64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (var aeDebug = reg64.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug", fix))
                {
                    var debugger = aeDebug.GetValue("Debugger", "") as string;
                    if (debugger != expectedJit)
                    {
                        if (fix)
                            aeDebug.SetValue("Debugger", expectedJit);
                        sbError.AppendLine($"- AeDebug\\Debugger (64 bit) is wrong ({debugger})");
                    }
                    var auto = aeDebug.GetValue("Auto", "").ToString();
                    if (auto != "1")
                    {
                        if (fix)
                            aeDebug.SetValue("Auto", "1");
                        sbError.AppendLine($"- AeDebug\\Auto (64 bit) is wrong ({auto})");
                    }
                }
            }

            if (!fix && sbError.Length > 0)
            {
                if (MessageBox.Show($"JitMagic is not (properly) installed:\n{sbError}\n\nDo you want to install now?", "Error", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = assembly,
                        UseShellExecute = true,
                        Verb = elevated ? "open" : "runas",
                        Arguments = "-fixregistry",
                    }).WaitForExit();
                }
            }
        }

        public JitMagic(string[] Args)
        {
            var jsonFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JitMagic.json");
            if (!File.Exists(jsonFile))
                File.WriteAllText(jsonFile, JsonConvert.SerializeObject(_jitDebuggers, Formatting.Indented));
            try
            {
                var json = File.ReadAllText(jsonFile);
                _jitDebuggers = JsonConvert.DeserializeObject<JitDebugger[]>(json);
            }
            catch
            {
            }

            CheckRegistry();

            InitializeComponent();
            KeyPreview = true;
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

            if (Args.Length == 6 && Args[0] == "-p" && Args[2] == "-e" && Args[4] == "-j")
            {
                _pid = int.Parse(Args[1]);
                _event = new IntPtr(int.Parse(Args[3]));
                _jdinfo = new IntPtr(long.Parse(Args[5].TrimStart('0', 'x', 'X'), System.Globalization.NumberStyles.HexNumber));
                Text = $"JitMagic - PID: {_pid}";
                try
                {
                    listViewDebuggers.ItemActivate += (s, e) =>
                    {
                        Hide();
                        var jitDebugger = _jitDebuggers[listViewDebuggers.SelectedItems[0].ImageIndex];

                        var sec = new SECURITY_ATTRIBUTES
                        {
                            length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                            securityDesc = IntPtr.Zero,
                            inherit = true
                        };
                        var hEvent = CreateEvent(ref sec, true, false, null);
                        var args = string.Format(jitDebugger.Arguments, _pid, hEvent.ToInt32(), _jdinfo.ToString("X"));
                        var psi = new ProcessStartInfo
                        {
                            UseShellExecute = false,
                            FileName = jitDebugger.FileName,
                            Arguments = args,
                        };
                        // Undocumented feature of vsjitdebugger.exe that will halt it until a debugger is attached.
                        //psi.EnvironmentVariables.Add("VS_Debugging_PauseOnStartup", "1");
                        var p = Process.Start(psi);
                        var handles = new HANDLES
                        {
                            h1 = hEvent,
                            h2 = p.Handle
                        };
                        WaitForMultipleObjects(2, ref handles, false, -1);
                        Close();
                    };

                    var process = Process.GetProcessById(_pid);
                    var architecture = GetProcessArchitecture(process);
                    labelProcessInformation.Text = $"{Path.GetFileName(GetProcessPath(_pid))} ({architecture})";
                    PopulateJitDebuggers(architecture);
                }
                catch (Exception x)
                {
                    MessageBox.Show("Error retrieving information! " + x);
                }
            }
            else
            {
                PopulateJitDebuggers(Architecture.All);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_event != IntPtr.Zero)
            {
                SetEvent(_event);
                CloseHandle(_event);
            }
            base.OnClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
            base.OnKeyDown(e);
        }

        void PopulateJitDebuggers(Architecture architecture)
        {
            listViewDebuggers.LargeImageList = new ImageList();
            for (var i = 0; i < _jitDebuggers.Length; i++)
            {
                var jitDebugger = _jitDebuggers[i];

                var icon = Icon.ExtractAssociatedIcon(jitDebugger.FileName);
                listViewDebuggers.LargeImageList.Images.Add(icon.ToBitmap());

                if (!File.Exists(jitDebugger.FileName))
                    continue;

                if (architecture == Architecture.All || jitDebugger.Architecture == architecture || jitDebugger.Architecture == Architecture.All)
                {
                    listViewDebuggers.Items.Add(new ListViewItem(jitDebugger.Name)
                    {
                        ImageIndex = i
                    });
                }
            }
            listViewDebuggers.Items[0].Selected = true;
            listViewDebuggers.Focus();
        }

        [JsonConverter(typeof(StringEnumConverter))]
        enum Architecture
        {
            x64,
            x86,
            All
        }

        static Architecture GetProcessArchitecture(Process p)
        {
            if (Environment.Is64BitOperatingSystem)
            {
                bool iswow64 = false;
                if (IsWow64Process(p.Handle, out iswow64))
                {
                    return iswow64 ? Architecture.x86 : Architecture.x64;
                }
                throw new Exception("IsWow64Process failed");
            }
            else
            {
                return Architecture.x86;
            }
        }

        static string GetProcessPath(int processId)
        {
            string MethodResult = "";
            try
            {
                string Query = "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;

                using (ManagementObjectSearcher mos = new ManagementObjectSearcher(Query))
                {
                    using (ManagementObjectCollection moc = mos.Get())
                    {
                        string ExecutablePath = (from mo in moc.Cast<ManagementObject>() select mo["ExecutablePath"]).First().ToString();
                        MethodResult = ExecutablePath;
                    }
                }
            }
            catch
            {
            }
            return MethodResult;
        }
    }
}
