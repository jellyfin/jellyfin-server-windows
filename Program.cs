using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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


	public class TrayApplicationContext : ApplicationContext
	{
		private readonly string _jellyfinServiceName = "JellyfinServer";
		private string _executableFile;
		private string _dataFolder = @"C:\ProgramData\Jellyfin\Server";
		private string _localJellyfinUrl = "http://localhost:8096";
		private NotifyIcon _trayIcon;
		private ServiceController _serviceController;
		private MenuItem _menuItemStart;
		private MenuItem _menuItemStop;
		private MenuItem _menuItemOpen;
		private MenuItem _menuItemExit;
		private string _installFolder;

		public TrayApplicationContext ()
		{
			_serviceController = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == _jellyfinServiceName);

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
			catch(Exception ex)
			{
				// We could not get the Jellyfin port from system.xml config file - just use default?
			}

			
			if (_serviceController == null)
			{
				try
				{
					_executableFile = Path.Combine(_installFolder, "jellyfin.exe");
					if (!File.Exists(_executableFile))
						throw new FileNotFoundException();
				}
				catch
				{
					// We could not find the Jellyfin executable file!
					MessageBox.Show("Could not find a Jellyfin Installation.");
					Application.Exit();
					return;
				}
			}
			
			_menuItemStart = new MenuItem("Start Jellyfin", Start);
			_menuItemStop = new MenuItem("Stop Jellyfin", Stop);
			_menuItemOpen = new MenuItem("Open Jellyfin", Open);
			_menuItemExit = new MenuItem("Exit", Exit);

			ContextMenu contextMenu = new ContextMenu(new[] {
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

		private void Open(object sender, EventArgs e)
		{
			Process.Start("explorer.exe", _localJellyfinUrl);
		}

		private void ContextMenuOnPopup(object sender, EventArgs e)
		{
			bool serviceExists = _serviceController != null;
			bool exeRunning = false;
			if (serviceExists)
				_serviceController.Refresh();
			else
				exeRunning = Process.GetProcessesByName("jellyfin").Length > 0;
			bool running = (!serviceExists && exeRunning) || (serviceExists && _serviceController.Status == ServiceControllerStatus.Running);
			bool stopped = (!serviceExists && !exeRunning) || (serviceExists && _serviceController.Status == ServiceControllerStatus.Stopped);
			_menuItemStart.Enabled = stopped;
			_menuItemStop.Enabled = running;
			_menuItemOpen.Enabled = running;
		}

		private void Start(object sender, EventArgs e)
		{
			if (_serviceController != null)
				_serviceController.Start();
			else
				Process.Start(_executableFile);
		}

		private void Stop(object sender, EventArgs e)
		{
			if (_serviceController != null)
				_serviceController.Stop();
			else
			{
				Process process = Process.GetProcessesByName("jellyfin").FirstOrDefault();
				if (process == null)
					return;
				if (process.CloseMainWindow())
					process.Kill();
			}
		}

		void Exit(object sender, EventArgs e)
		{
			_trayIcon.Visible = false;
			Application.Exit();
		}
	}
}
