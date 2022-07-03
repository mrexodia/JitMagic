using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JitMagic
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] Args)
        {
            if (Args.Length == 1 && Args[0] == "-fixregistry")
            {
                try
                {
                    JitMagic.CheckRegistry(true);
                }
                catch (Exception x)
                {
                    MessageBox.Show($"Failed to fix registry: {x}");
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var jitMagic = new JitMagic(Args);
            Application.Run(jitMagic);
        }
    }
}
