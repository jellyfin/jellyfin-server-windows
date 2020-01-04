using System;
using System.Windows.Forms;

namespace Jellyfin.Windows.Tray
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new TrayApplicationContext());
        }
    }
}
