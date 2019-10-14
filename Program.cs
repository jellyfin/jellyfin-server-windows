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
		private string _dataFolder = @"C:\ProgramData\Jellyfin\Server";
		private string _localJellyfinUrl = "http://localhost:8096";
		private NotifyIcon _trayIcon;
		private ServiceController _serviceController;
		private MenuItem _menuItemService;
		private MenuItem _menuItemStart;
		private MenuItem _menuItemStop;
		private MenuItem _menuItemOpen;
		private MenuItem _menuItemExit;

		public TrayApplicationContext ()
		{
			_serviceController = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == _jellyfinServiceName);

			if (_serviceController != null)
			{
				try
				{
					ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
					ManagementObjectCollection collection = searcher.Get();
					ManagementObject managementObject = collection.OfType<ManagementObject>().First(o => o["Name"] as string == _jellyfinServiceName);
					string pathName = managementObject["PathName"] as string;
					string appParameters = StartGetOutput(pathName, $"get {_jellyfinServiceName} AppParameters");
					Regex dataDirRegex = new Regex("--datadir \"(.*?)\"");
					Match match = dataDirRegex.Match(appParameters);
					if (match.Success)
					{
						_dataFolder = match.Groups[1].Value;
					}

					XDocument systemXml = XDocument.Load(Path.Combine(_dataFolder, "config\\system.xml"));
					string port = systemXml.CreateNavigator().SelectSingleNode("/ServerConfiguration/PublicPort").Value;
					_localJellyfinUrl = "http://localhost:" + port;
				}
				catch(Exception ex)
				{
					// We could not get the jellyfin port from system.xml config file - just use default?
				}
			}

			_menuItemService = new MenuItem("Jellyfin Service not found!");
			_menuItemService.Enabled = false;
			_menuItemStart = new MenuItem("Start Jellyfin", Start);
			_menuItemStop = new MenuItem("Stop Jellyfin", Stop);
			_menuItemOpen = new MenuItem("Open Jellyfin", Open);
			_menuItemExit = new MenuItem("Exit", Exit);

			ContextMenu contextMenu = new ContextMenu(new MenuItem[] {
																		 _menuItemService,
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
			_menuItemService.Visible = !serviceExists;
			bool running = serviceExists && _serviceController.Status == ServiceControllerStatus.Running;
			bool stopped = serviceExists && _serviceController.Status == ServiceControllerStatus.Stopped;
			_menuItemStart.Enabled = stopped;
			_menuItemStop.Enabled = running;
			_menuItemOpen.Enabled = running;
		}

		private void Start(object sender, EventArgs e)
		{
			_serviceController.Start();
		}

		private void Stop(object sender, EventArgs e)
		{
			_serviceController.Stop();
		}

		void Exit(object sender, EventArgs e)
		{
			_trayIcon.Visible = false;
			Application.Exit();
		}

		private string StartGetOutput(string filename, string arguments)
		{
			Process process= new Process();
			process.StartInfo.FileName = filename;
			process.StartInfo.Arguments = arguments;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();

			string output = process.StandardOutput.ReadToEnd();

			process.WaitForExit();

			return output;
		}
	}
}
