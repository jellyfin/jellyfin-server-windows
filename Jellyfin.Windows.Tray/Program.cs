using System;
using System.Threading;
using System.Windows.Forms;

namespace Jellyfin.Windows.Tray
{
    /// <summary>
    /// The main program.
    /// </summary>
    public static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            if (IsAlreadyRunning())
            {
                MessageBox.Show("The Jellyfin tray application is already running.", "Info", new MessageBoxButtons { }, MessageBoxIcon.Information);
                Environment.Exit(1);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            var trayApplicationContext = new TrayApplicationContext();
            if (trayApplicationContext.InitApplication())
            {
                Application.Run(trayApplicationContext);
            }
        }

        private static bool IsAlreadyRunning()
        {
            _mutex = new Mutex(true, "Jellyfin.Windows.Tray", out bool createdNew);

            return !createdNew;
        }
    }
}
