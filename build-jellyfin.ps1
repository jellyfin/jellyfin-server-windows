[CmdletBinding()]
param(
    [switch]$InstallFFMPEG,
    [switch]$SkipJellyfinBuild,
    [switch]$GenerateZip,
    [string]$InstallLocation = "$Env:AppData\Jellyfin-Server\",
    [ValidateSet('Debug', 'Release')][string]$BuildType = 'Release',
    [ValidateSet('Quiet', 'Minimal', 'Normal')][string]$DotNetVerbosity = 'Minimal',
    [ValidateSet('win', 'win7', 'win8', 'win81', 'win10')][string]$WindowsVersion = 'win',
    [ValidateSet('x64', 'arm', 'arm64')][string]$Architecture = 'x64'
)

# Speed up all downloads by hiding progress bars
$ProgressPreference = 'SilentlyContinue'

# PowershellCore and *nix check to determine directory
if(($PSVersionTable.PSEdition -eq 'Core') -and (-not $IsWindows)){
    $TempDir = mktemp -d
}else{
    $TempDir = $env:Temp
}
# Create staging directory
New-Item -ItemType Directory -Force -Path $InstallLocation

$ResolvedInstallLocation = Resolve-Path $InstallLocation

function Build-Jellyfin {
    if(($Architecture -eq 'arm64') -and ($WindowsVersion -ne 'win10')){
        Write-Error "arm64 only supported with Windows 10 Version"
        exit
    }
    if(($Architecture -eq 'arm') -and ($WindowsVersion -notin @('win10','win81','win8'))){
        Write-Error "arm only supported with Windows 8 or higher"
        exit
    }
    Write-Verbose "windowsversion-Architecture: $windowsversion-$Architecture"
    Write-Verbose "InstallLocation: $ResolvedInstallLocation"
    Write-Verbose "DotNetVerbosity: $DotNetVerbosity"
    dotnet publish --self-contained -c $BuildType --output $ResolvedInstallLocation -v $DotNetVerbosity -p:GenerateDocumentationFile=false -p:DebugSymbols=false -p:DebugType=none --runtime `"$windowsversion-$Architecture`" Jellyfin.Server
}

function Install-FFMPEG {
    param(
        [string]$ResolvedInstallLocation,
        [string]$Architecture
    )

    Write-Verbose "Checking Architecture"
    if($Architecture -notin @('x64')){
        Write-Warning "No builds available for your selected architecture of $Architecture"
        Write-Warning "FFMPEG will not be installed."
    }elseif($Architecture -eq 'x64'){
         Write-Verbose "Downloading 64 bit FFMPEG"
         Invoke-WebRequest -Uri https://repo.jellyfin.org/releases/server/windows/ffmpeg/jellyfin-ffmpeg.zip -UseBasicParsing -OutFile "$tempdir/ffmpeg.zip" | Write-Verbose
       Expand-Archive "$tempdir/ffmpeg.zip" -DestinationPath "$tempdir/ffmpeg/" -Force | Write-Verbose      
    }


    if($Architecture -eq 'x64'){
        Write-Verbose "Copying Binaries to Jellyfin location"
        Get-ChildItem "$tempdir/ffmpeg" | ForEach-Object {
            Copy-Item $_.FullName -Destination $installLocation | Write-Verbose
        }
    }
    
    Remove-Item "$tempdir/ffmpeg/" -Recurse -Force -ErrorAction Continue | Write-Verbose
    Remove-Item "$tempdir/ffmpeg.zip" -Force -ErrorAction Continue | Write-Verbose
}


if(-not $SkipJellyfinBuild.IsPresent -and -not ($InstallNSIS -eq $true)){
    Write-Verbose "Starting Build Process: Selected Environment is $WindowsVersion-$Architecture"
    Build-Jellyfin
}
if($InstallFFMPEG.IsPresent -or ($InstallFFMPEG -eq $true)){
    Write-Verbose "Starting FFMPEG Install"
    Install-FFMPEG $ResolvedInstallLocation $Architecture
}

if($GenerateZip.IsPresent -or ($GenerateZip -eq $true)){
    Compress-Archive -Path $ResolvedInstallLocation -DestinationPath "$ResolvedInstallLocation/jellyfin.zip" -Force
}
Write-Verbose "Finished"
