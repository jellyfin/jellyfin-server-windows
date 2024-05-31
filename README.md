<h1 align="center">Jellyfin for Windows</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

---

<p align="center">
<img alt="Logo Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/branding/SVG/banner-logo-solid.svg?sanitize=true"/>
<br/>
<br/>
<a href="https://github.com/jellyfin/jellyfin-server-windows/blob/master/LICENSE">
<img alt="MIT License" src="https://img.shields.io/github/license/jellyfin/jellyfin-server-windows.svg"/>
</a>
<a href="https://opencollective.com/jellyfin">
<img alt="Donate" src="https://img.shields.io/opencollective/all/jellyfin.svg?label=backers"/>
</a>
<a href="https://features.jellyfin.org">
<img alt="Submit Feature Requests" src="https://img.shields.io/badge/fider-vote%20on%20features-success.svg"/>
</a>
<a href="https://matrix.to/#/+jellyfin:matrix.org">
<img alt="Chat on Matrix" src="https://img.shields.io/matrix/jellyfin:matrix.org.svg?logo=matrix"/>
</a>
<a href="https://www.reddit.com/r/jellyfin">
<img alt="Join our Subreddit" src="https://img.shields.io/badge/reddit-r%2Fjellyfin-%23FF5700.svg"/>
</a>
<a href="https://github.com/jellyfin/jellyfin-server-windows/commits/master.atom">
<img alt="Commits RSS Feed" src="https://img.shields.io/badge/rss-commits-ffa500?logo=rss" />
</a>
</p>

---

Jellyfin for Windows collects the tray application, service utilities, and NSIS installer that are used when setting up and running Jellyfin.

<br/>

# Getting Started
Are you looking to just run and setup Jellyfin on your Windows machine? Go to https://jellyfin.org/downloads and get the Windows stable release.

Do you want to build Jellyfin's tray app or installer for yourself? Read on!

---

## Compiling the Tray App
### Requirements
* [.NET 6.0 SDK](https://dotnet.microsoft.com/download)
    * **NOTE**: This SDK should always match the version that is currently in use for the server.

### Steps
1. Build using the dotnet command, or using Visual Studio/VS Code.
    * On the command line, in the root of the cloned repository, execute this command: `dotnet build -c Release -f net472`
2. From the resulting bin folder, collect `Jellyfin.Windows.Tray.exe` and all the DLLs within.
3. For use with a Jellyfin install, place in its own directory, such as `jellyfin-windows-tray`.

### Usage
The tray app is designed to do three things:
1. Start and Stop Jellyfin
2. Open the Web UI
3. Open the Log Folder

To control Jellyfin, it expects that either Jellyfin is installed as a service, or that a corresponding set of registry keys has been set by the installer.

The registry entries look like the following in a typical install:

Location: `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Jellyfin\Server`
| Name               | Type          | Data                                |
| ------------------ | ------------- | ----------------------------------- |
| DataFolder         | REG_EXPAND_SZ | C:\\ProgramData\\Jellyfin\\Server   |
| InstallFolder      | REG_EXPAND_SZ | C:\\Program Files\\Jellyfin\\Server |
| ServiceAccountType | REG_SZ        | None                                |

* DataFolder must be the location where the application support files will go (database, config, logs, etc).
* InstallFolder must be the location where ` jellyfin.exe` can be found.
* ServiceAccountType is "None" unless Jellyfin is installed as a service, in which case it will either be "LocalSystem" or "NetworkService".

If you want to quickly import these default paths, you can use `Jellyfin Registry.reg` in the `Support Files` folder to do so.

When the tray app is started, it will check if Jellyfin is installed as a service with the name `JellyfinServer`, and if located it will start the service (unless it is already running). If the server is not installed as a service, it will look to the registry for the location of the config files and executable, and launch the executable with the data folder path as an argument. If the registry keys are not found, it will close with an error that an installation was not located.

To open the Web UI, the app will look for a network config in the DataFolder, and open the user's default browser to the path. To open the Log Folder, the app will launch Windows Explorer to the DataFolder path, appending `\Log` to the end.

## Building the Installer
### Requirements
* The compiled tray app from above
* [NSIS 3.x+](https://nsis.sourceforge.io/Download)
* A copy of the [jellyfin-ux](https://github.com/jellyfin/jellyfin-ux) repository
* The latest [Jellyfin Windows Combined](https://repo.jellyfin.org/releases/server/windows/versions/stable/combined/) package
* The [GPLv2 License](https://www.gnu.org/licenses/old-licenses/gpl-2.0.txt) as a file simply named LICENSE

If you choose to build Jellyfin server on your own, you will also require:
* [jellyfin-ffmpeg](https://repo.jellyfin.org/releases/server/windows/versions/jellyfin-ffmpeg/) for Windows, or equivalent FFmpeg/FFprobe 4.3.2+

### Steps
1. Ensure that a complete copy of Jellyfin Server is available in a folder. If using the combined package from above, proceed to the next step.
    * If you are building Jellyfin from source, place a copy of `jellyfin-ffmpeg` or equivalent in the same folder as the server binary. You need to add `ffmpeg.exe` and `ffprobe.exe` alongside `jellyfin.exe`.
2. Copy the GPLv2 License file either from their website, or the `Support Files` folder, and place it in the same directory as the server. Ensure that it is named `LICENSE` with no extension.
3. Copy the contents of the compiled tray app (including its DLLs) into the folder with the server. If there is a duplicate DLL, skip it. We only need to add anything that isn't already included.
4. Download a copy of the `jellyfin-ux` repository, or at least have the following files at a path ending with `\branding\NSIS\`:
    * modern-install.ico
    * installer-header.bmp
    * installer-right.bmp
5. Install NSIS if not already available. Be sure to select a Full install.
6. Open Powershell. Set the environment variable `InstallLocation` to the folder where Jellyfin Server is available.
    * e.g. `$env:InstallLocation = "C:\Users\Anthony\Downloads\jellyfin_10.7.7"`
7. Go to the directory where NSIS is installed. In most systems, this is at `C:\Program Files (x86)`.
    * e.g. `cd 'C:\Program Files (x86)\NSIS'`.
8. Run the following command, substituting the path to your `jellyfin-ux` files and the NSIS script from this repository:

    ```
    .\makensis /Dx64 /DUXPATH=C:\Users\Anthony\Downloads\jellyfin-ux-master "C:\Users\Anthony\Downloads\jellyfin-server-windows\nsis\jellyfin.nsi"
    ```

8. Wait for the installer to build. When complete, it will be located next to the NSIS script file. It is now ready to be used.

# Troubleshooting
If you have any questions or encounter any problems, please [open an issue](https://github.com/jellyfin/jellyfin-server-windows/issues/new) in this repository, or reach out to [@anthonylavado](https://github.com/anthonylavado) on [Matrix/Discord/IRC](https://jellyfin.org/contact/).
