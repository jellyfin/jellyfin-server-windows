using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using Jellyfin.Windows.Tray.Properties;
using Microsoft.Win32;

namespace Jellyfin.Windows.Tray
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly string _jellyfinServiceName = "JellyfinServer";
        private readonly string _autostartKey = "JellyfinTray";
        private string _configFile;
        private bool FirstRunDone = false;
        private string _executableFile;
        private string _dataFolder = @"C:\ProgramData\Jellyfin\Server";
        private string _localJellyfinUrl = "http://localhost:8096/web/index.html";
        private NotifyIcon _trayIcon;
        private ServiceController _serviceController;
        private MenuItem _menuItemAutostart;
        private MenuItem _menuItemStart;
        private MenuItem _menuItemStop;
        private MenuItem _menuItemOpen;
        private MenuItem _menuItemLogFolder;
        private MenuItem _menuItemExit;
        private string _installFolder;
        private RunType _runType;

        public TrayApplicationContext()
        {
            LoadPortFromJellyfinConfig();

            _serviceController = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == _jellyfinServiceName);
            if (_serviceController == null)
            {
                _executableFile = Path.Combine(_installFolder, "jellyfin.exe");
                if (!File.Exists(_executableFile))
                {
                    // We could not find the Jellyfin executable file!
                    MessageBox.Show("Could not find a Jellyfin Installation.");
                    Application.Exit();
                    return;
                }

                _runType = RunType.Executable;
            }
            else
            {
                _runType = RunType.Service;
            }

            CreateTrayIcon();


            if (!FirstRunDone)
            {
                CheckShowServiceNotElevatedWarning();
                AutoStart = true;
            }
            else
                FirstRunDone = true;


            if (_runType == RunType.Executable)
            {
                // check if Jellyfin is already runnning, is not start it
                if (Process.GetProcessesByName("jellyfin").Length == 0)
                    Start(null, null);
            }
        }

        private void CreateTrayIcon()
        {
            _menuItemAutostart = new MenuItem("Autostart", AutoStartToggle);
            _menuItemStart = new MenuItem("Start Jellyfin", Start);
            _menuItemStop = new MenuItem("Stop Jellyfin", Stop);
            _menuItemOpen = new MenuItem("Open Jellyfin", Open);
            _menuItemLogFolder = new MenuItem("Show Logs", ShowLogs);
            _menuItemExit = new MenuItem("Exit", Exit);

            ContextMenu contextMenu = new ContextMenu(new[]
                                                      {
                                                          _menuItemAutostart,
                                                          new MenuItem("-"),
                                                          _menuItemStart,
                                                          _menuItemStop,
                                                          new MenuItem("-"),
                                                          _menuItemOpen,
                                                          new MenuItem("-"),
                                                          _menuItemLogFolder,
                                                          new MenuItem("-"),
                                                          _menuItemExit
                                                      });
            contextMenu.Popup += ContextMenuOnPopup;
            _trayIcon = new NotifyIcon()
            {
                Icon = Resources.JellyfinIcon,
                ContextMenu = contextMenu,
                Visible = true
            };
        }

        private void LoadPortFromJellyfinConfig()
        {
            try
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("Software\\Jellyfin\\Server");
                // registry keys are probably written by a 32bit installer, so check the 32bit registry folder
                if (registryKey == null)
                    registryKey = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Jellyfin\\Server");
                _installFolder = registryKey.GetValue("InstallFolder").ToString();
                _dataFolder = registryKey.GetValue("DataFolder").ToString();
                _configFile = Path.Combine(_dataFolder, "config\\system.xml").ToString();


                if (File.Exists(_configFile))
                {
                    XDocument systemXml = XDocument.Load(_configFile);
                    XPathNavigator SettingsReader = systemXml.CreateNavigator();

                    FirstRunDone = SettingsReader.SelectSingleNode("/ServerConfiguration/IsStartupWizardCompleted").ValueAsBoolean;
                    string port = SettingsReader.SelectSingleNode("/ServerConfiguration/PublicPort").Value;

                    _localJellyfinUrl = "http://localhost:" + port + "/web/index.html";
                }
            }
            catch (Exception ex)
            {
                // We could not get the Jellyfin port from system.xml config file - just use default?
                MessageBox.Show("Error: " + ex.Message + "\r\nCouldn't load configuration. The application will now close.");
                Application.Exit();
                return;
            }
        }

        private bool CheckShowServiceNotElevatedWarning()
        {
            if (_runType == RunType.Service && !IsElevated())
            {
                MessageBox.Show("When running Jellyfin as a service the tray application must be run as Administrator");
                return true;
            }
            return false;
        }

        private bool IsElevated()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            return id.Owner != id.User;
        }

        private void AutoStartToggle(object sender, EventArgs e)
        {
            AutoStart = !AutoStart;
        }

        private void Open(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", _localJellyfinUrl);
        }
        private void ShowLogs(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", _dataFolder + "\\log");
        }
        private void ContextMenuOnPopup(object sender, EventArgs e)
        {
            bool runningAsService = _runType == RunType.Service;
            bool exeRunning = false;
            if (runningAsService)
                _serviceController.Refresh();
            else
                exeRunning = Process.GetProcessesByName("jellyfin").Length > 0;
            bool running = (!runningAsService && exeRunning) || (runningAsService && _serviceController.Status == ServiceControllerStatus.Running);
            bool stopped = (!runningAsService && !exeRunning) || (runningAsService && _serviceController.Status == ServiceControllerStatus.Stopped);
            _menuItemStart.Enabled = stopped;
            _menuItemStop.Enabled = running;
            _menuItemOpen.Enabled = running;
            _menuItemAutostart.Checked = AutoStart;
        }

        private void Start(object sender, EventArgs e)
        {
            if (CheckShowServiceNotElevatedWarning())
                return;
            if (_runType == RunType.Service)
                _serviceController.Start();
            else
            {
                Process p = new Process();
                p.StartInfo.FileName = _executableFile;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.Arguments = "--noautorunwebapp --datadir \"" + _dataFolder + "\"";
                p.Start();
            }
        }

        private void Stop(object sender, EventArgs e)
        {
            if (CheckShowServiceNotElevatedWarning())
                return;
            if (_runType == RunType.Service)
                _serviceController.Stop();
            else
            {
                Process process = Process.GetProcessesByName("jellyfin").FirstOrDefault();
                if (process == null)
                    return;
                if (!process.CloseMainWindow())
                    process.Kill();
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            if (_runType == RunType.Executable)
            {
                Stop(null, null);
            }

            _trayIcon.Visible = false;
            Application.Exit();
        }

        private bool AutoStart
        {
            get
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                return key.GetValue(_autostartKey) != null;
            }
            set
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (value && key.GetValue(_autostartKey) == null)
                {
                    key.SetValue(_autostartKey, Path.ChangeExtension(Application.ExecutablePath, "exe"));
                }
                else if (!value && key.GetValue(_autostartKey) != null)
                {
                    key.DeleteValue(_autostartKey);
                }
            }
        }
    }
}
