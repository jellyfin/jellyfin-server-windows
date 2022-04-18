using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;

namespace Jellyfin.Windows.Tray;

/// <summary>
/// Tray application context.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private const string TrayIconResourceName = "Jellyfin.Windows.Tray.Resources.JellyfinIcon.ico";
    private readonly string _jellyfinServiceName = "JellyfinServer";
    private readonly string _autostartKey = "JellyfinTray";
    private string _configFile;
    private string _networkFile;
    private string _port;
    private bool _firstRunDone = false;
    private string _networkAddress;
    private string _executableFile;
    private string _dataFolder = @"C:\ProgramData\Jellyfin\Server";
    private string _localJellyfinUrl = "http://localhost:8096/web/index.html";
    private NotifyIcon _trayIcon;
    private ServiceController _serviceController;
    private ToolStripMenuItem _menuItemAutostart;
    private ToolStripMenuItem _menuItemStart;
    private ToolStripMenuItem _menuItemStop;
    private ToolStripMenuItem _menuItemOpen;
    private ToolStripMenuItem _menuItemLogFolder;
    private ToolStripMenuItem _menuItemExit;
    private string _installFolder;
    private RunType _runType;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayApplicationContext"/> class.
    /// </summary>
    public TrayApplicationContext()
    {
        _serviceController = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == _jellyfinServiceName);
        RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Jellyfin\\Server");
        if (_serviceController != null)
        {
            _runType = RunType.Service;
        }

        if (_serviceController == null)
        {
            try
            {
                LoadJellyfinConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + "\r\nCouldn't find Jellyfin Installation. The application will now close.");
                Application.Exit();
                return;
            }

            _runType = RunType.Executable;
        }

        CreateTrayIcon();

        if (!_firstRunDone)
        {
            CheckShowServiceNotElevatedWarning();
            AutoStart = true;
        }
        else
        {
            _firstRunDone = true;
        }

        if (_runType == RunType.Executable)
        {
            // check if Jellyfin is already running, if not, start it
            if (Process.GetProcessesByName("jellyfin").Length == 0)
            {
                Start(null, null);
            }
        }
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

    private void CreateTrayIcon()
    {
        _menuItemAutostart = new ToolStripMenuItem("Autostart", null, AutoStartToggle);
        _menuItemStart = new ToolStripMenuItem("Start Jellyfin", null, Start);
        _menuItemStop = new ToolStripMenuItem("Stop Jellyfin", null, Stop);
        _menuItemOpen = new ToolStripMenuItem("Open Jellyfin", null, Open);
        _menuItemLogFolder = new ToolStripMenuItem("Show Logs", null, ShowLogs);
        _menuItemExit = new ToolStripMenuItem("Exit", null, Exit);

        ContextMenuStrip contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_menuItemAutostart);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_menuItemStart);
        contextMenu.Items.Add(_menuItemStop);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_menuItemOpen);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_menuItemLogFolder);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_menuItemExit);

        contextMenu.Opening += new CancelEventHandler(ContextMenuOnPopup);
        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TrayIconResourceName);
        _trayIcon = new NotifyIcon() { Icon = new Icon(iconStream), ContextMenuStrip = contextMenu, Visible = true };
    }

    private void LoadJellyfinConfig()
    {
        RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Jellyfin\\Server");
        _installFolder = registryKey.GetValue("InstallFolder").ToString();
        _dataFolder = registryKey.GetValue("DataFolder").ToString();
        _configFile = Path.Combine(_dataFolder, "config\\system.xml").ToString();
        _networkFile = Path.Combine(_dataFolder, "config\\network.xml").ToString();
        _executableFile = Path.Combine(_installFolder, "jellyfin.exe");
        _port = "8096";

        if (File.Exists(_configFile))
        {
            XDocument systemXml = XDocument.Load(_configFile);
            XPathNavigator settingsReader = systemXml.CreateNavigator();

            _firstRunDone = settingsReader.SelectSingleNode("/ServerConfiguration/IsStartupWizardCompleted").ValueAsBoolean;
            var publicPort = settingsReader.SelectSingleNode("/ServerConfiguration/PublicPort")?.Value;
            if (!string.IsNullOrEmpty(publicPort))
            {
                _port = publicPort;
            }
        }

        if (File.Exists(_networkFile))
        {
            XDocument networkXml = XDocument.Load(_networkFile);
            XPathNavigator networkReader = networkXml.CreateNavigator();

            _networkAddress = networkReader.SelectSingleNode("/NetworkConfiguration/LocalNetworkAddresses").Value;
        }

        if (string.IsNullOrEmpty(_networkAddress))
        {
            _networkAddress = "localhost";
        }

        _localJellyfinUrl = "http://" + _networkAddress + ":" + _port + "/web/index.html";
    }

    private bool CheckShowServiceNotElevatedWarning()
    {
        if (_runType == RunType.Service && !IsElevated())
        {
            MessageBox.Show("When running Jellyfin as a service, the tray application must be run as Administrator.");
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
        {
            _serviceController.Refresh();
        }
        else
        {
            exeRunning = Process.GetProcessesByName("jellyfin").Length > 0;
        }

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
        {
            return;
        }

        if (_runType == RunType.Service)
        {
            _serviceController.Start();
        }
        else
        {
            Process p = new Process();
            p.StartInfo.FileName = _executableFile;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.Arguments = "--datadir \"" + _dataFolder + "\"";
            p.Start();
        }
    }

    private void Stop(object sender, EventArgs e)
    {
        if (CheckShowServiceNotElevatedWarning())
        {
            return;
        }

        if (_runType == RunType.Service)
        {
            _serviceController.Stop();
        }
        else
        {
            Process process = Process.GetProcessesByName("jellyfin").FirstOrDefault();
            if (process == null)
            {
                return;
            }

            if (!process.CloseMainWindow())
            {
                process.Kill();
            }
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
}
