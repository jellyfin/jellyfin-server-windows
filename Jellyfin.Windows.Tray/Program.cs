using System;
using System.Windows.Forms;

namespace Jellyfin.Windows.Tray
{
    /// <summary>
    /// The main program.
    /// </summary>
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var trayApplicationContext = new TrayApplicationContext();
            if (trayApplicationContext.InitApplication())
            {
                Application.Run(trayApplicationContext);
            }
        }
    }
}
