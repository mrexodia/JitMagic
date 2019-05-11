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

        JitDebugger[] _jitDebuggers = new[]
        {
            new JitDebugger("Visual Studio", Architecture.x86)
            {
                FileName = @"C:\Windows\SysWOW64\vsjitdebugger.exe",
                Arguments = "-p {0} -e {1}"
            },
            new JitDebugger("Visual Studio", Architecture.x64)
            {
                FileName = @"C:\Windows\System32\vsjitdebugger.exe",
                Arguments = "-p {0} -e {1}"
            },
            new JitDebugger("WinDbg (x86)", Architecture.x86)
            {
                FileName = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\windbg.exe",
                Arguments = "-p {0} -e {1} -g"
            },
            new JitDebugger("WinDbg (x64)", Architecture.x64)
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

            InitializeComponent();
            KeyPreview = true;
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

            if (Args.Length == 4 && Args[0] == "-p" && Args[2] == "-e")
            {
                _pid = int.Parse(Args[1]);
                _event = new IntPtr(int.Parse(Args[3]));
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
                        var args = string.Format(jitDebugger.Arguments, _pid, hEvent.ToInt32());
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
                catch
                {
                    labelProcessInformation.Text = "Error retrieving information!";
                }
            }
            else
            {
                PopulateJitDebuggers(Architecture.All);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if(_event != IntPtr.Zero)
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
                if(IsWow64Process(p.Handle, out iswow64))
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
