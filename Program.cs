using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using JellyfinTray.Properties;
using Microsoft.Win32;


namespace JellyfinTray
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			Application.Run(new TrayApplicationContext());
		}
	}

	enum RunType
	{
		Service,
		Executable
	}

	public class TrayApplicationContext : ApplicationContext
	{
		private readonly string _jellyfinServiceName = "JellyfinServer";
		private readonly string _autostartKey = "JellyfinTray";
		private readonly string _settingsFile = "JFTray.json";
		private string _executableFile;
		private string _dataFolder = @"C:\ProgramData\Jellyfin\Server";
		private string _localJellyfinUrl = "http://localhost:8096";
		private NotifyIcon _trayIcon;
		private ServiceController _serviceController;
		private MenuItem _menuItemAutostart;
		private MenuItem _menuItemStart;
		private MenuItem _menuItemStop;
		private MenuItem _menuItemOpen;
		private MenuItem _menuItemExit;
		private string _installFolder;
		private RunType _runType;
		private AppSettings _settings;

		public TrayApplicationContext ()
		{
			LoadPortFromJellyfinConfig();
			
			_serviceController = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == _jellyfinServiceName);
			if (_serviceController == null)
			{
				try
				{
					_executableFile = Path.Combine(_installFolder, "jellyfin.exe");
					if (!File.Exists(_executableFile))
						throw new FileNotFoundException();
					_runType = RunType.Executable;
				}
				catch
				{
					// We could not find the Jellyfin executable file!
					MessageBox.Show("Could not find a Jellyfin Installation.");
					Application.Exit();
					return;
				}
			}
			else
				_runType = RunType.Service;
			
			CreateTrayIcon();

			LoadSettings();

			if (!_settings.FirstRunDone)
			{
				CheckShowServiceNotElevatedWarning();
				AutoStart = true;
			}
			_settings.FirstRunDone = true;
			SaveSettings();

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
				XDocument systemXml = XDocument.Load(Path.Combine(_dataFolder, "config\\system.xml"));
				string port = systemXml.CreateNavigator().SelectSingleNode("/ServerConfiguration/PublicPort").Value;
				_localJellyfinUrl = "http://localhost:" + port;
			}
			catch (Exception ex)
			{
				// We could not get the Jellyfin port from system.xml config file - just use default?
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
                p.StartInfo.Arguments = "--noautorunwebapp";
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

		void Exit(object sender, EventArgs e)
		{
			if (_runType == RunType.Executable)
				Stop(null, null);
			_trayIcon.Visible = false;
			Application.Exit();
		}

		private void SaveSettings()
		{
			File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings));
		}

		private void LoadSettings()
		{
			if (File.Exists(_settingsFile))
				_settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFile));
			else
				_settings = new AppSettings();
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
					key.SetValue(_autostartKey, Path.ChangeExtension(Application.ExecutablePath, "exe"));
				else if (!value && key.GetValue(_autostartKey) != null)
					key.DeleteValue(_autostartKey);
			}
		}
	}

	class AppSettings
	{
		public bool FirstRunDone = false;
	}
}
