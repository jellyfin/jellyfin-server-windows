param (
    [string] $BuildOption = ""
)

# Fail early on all errors
$ErrorActionPreference = "Stop"

# Invoke-WebRequest is really slow by default because it renders a progress bar.
# Disabling this, improves vastly performance:
$ProgressPreference = 'SilentlyContinue'

# change directory to the directory above (if needed)
Set-Location -Path "$PSScriptRoot\..\"

# Global constants
$RootPath = "$PWD"
$BuildPath = "$RootPath\build"
$DeployPath = "$RootPath\deploy"
$WindowsPath ="$RootPath\windows"

# Execute native command with errorlevel handling
Function Invoke-Native-Command {
    param(
        [string] $Command,
        [string[]] $Arguments
    )

    & "$Command" @Arguments

    if ($LastExitCode -Ne 0)
    {
        Throw "Native command $Command returned with exit code $LastExitCode"
    }
}

# Cleanup existing build folders
Function Clean-Build-Environment
{
    if (Test-Path -Path $BuildPath) { Remove-Item -Path $BuildPath -Recurse -Force }
    if (Test-Path -Path $DeployPath) { Remove-Item -Path $DeployPath -Recurse -Force }

    New-Item -Path $BuildPath -ItemType Directory
    New-Item -Path $DeployPath -ItemType Directory
}

# For sourceforge links we need to get the correct mirror (especially NISIS) Thanks: https://www.powershellmagazine.com/2013/01/29/pstip-retrieve-a-redirected-url/
Function Get-RedirectedUrl {
    param(
        [Parameter(Mandatory=$true)]
        [string] $url
    )

    $numAttempts = 10
    $sleepTime = 10
    $maxSleepTime = 80
    for ($attempt = 1; $attempt -le $numAttempts; $attempt++) {
        try {
            $request = [System.Net.WebRequest]::Create($url)
            $request.AllowAutoRedirect=$true
            $response=$request.GetResponse()
            $response.ResponseUri.AbsoluteUri
            $response.Close()
            return
        } catch {
            if ($attempt -lt $numAttempts) {
                Write-Warning "Caught error: $_"
                Write-Warning "Get-RedirectedUrl: Fetch attempt #${attempt}/${numAttempts} for $url failed, trying again in ${sleepTime}s"
                Start-Sleep -Seconds $sleepTime
                $sleepTime = [Math]::Min($sleepTime * 2, $maxSleepTime)
                continue
            }
            Write-Error "Get-RedirectedUrl: All ${numAttempts} fetch attempts for $url failed, failing whole call"
            throw
        }
    }
}

function Initialize-Module-Here ($m) { # see https://stackoverflow.com/a/51692402

    # If module is imported say that and do nothing
    if (Get-Module | Where-Object {$_.Name -eq $m}) {
        Write-Output "Module $m is already imported."
    }
    else {

        # If module is not imported, but available on disk then import
        if (Get-Module -ListAvailable | Where-Object {$_.Name -eq $m}) {
            Import-Module $m
        }
        else {

            # If module is not imported, not available on disk, but is in online gallery then install and import
            if (Find-Module -Name $m | Where-Object {$_.Name -eq $m}) {
                Install-Module -Name $m -Force -Verbose -Scope CurrentUser
                Import-Module $m
            }
            else {

                # If module is not imported, not available and not in online gallery then abort
                Write-Output "Module $m not imported, not available and not in online gallery, exiting."
                EXIT 1
            }
        }
    }
}

# Download and uncompress dependency in ZIP format
Function Install-Dependency
{
    param(
        [Parameter(Mandatory=$true)]
        [string] $Uri,
        [Parameter(Mandatory=$true)]
        [string] $Name,
        [Parameter(Mandatory=$true)]
        [string] $Destination
    )

    if (Test-Path -Path "$WindowsPath\$Destination")
    {
        echo "Using ${WindowsPath}\${Destination} from previous run (e.g. actions/cache)"
        return
    }

    $TempFileName = [System.IO.Path]::GetTempFileName() + ".zip"
    $TempDir = [System.IO.Path]::GetTempPath()

    if ($Uri -Match "downloads.sourceforge.net")
    {
      $Uri = Get-RedirectedUrl -URL $Uri
    }

    Invoke-WebRequest -Uri $Uri -OutFile $TempFileName
    echo $TempFileName
    Expand-Archive -Path $TempFileName -DestinationPath $TempDir -Force
    echo $WindowsPath\$Destination
    Move-Item -Path "$TempDir\$Name" -Destination "$WindowsPath\$Destination" -Force
    Remove-Item -Path $TempFileName -Force
}

# Install VSSetup (Visual Studio detection), ASIO SDK and NSIS Installer
Function Install-Dependencies
{
    if (-not (Get-PackageProvider -Name nuget).Name -eq "nuget") {
      Install-PackageProvider -Name "Nuget" -Scope CurrentUser -Force
    }
    Initialize-Module-Here -m "VSSetup"
}

# Setup environment variables and build tool paths
Function Initialize-Build-Environment
{
    param(
        [Parameter(Mandatory=$true)]
        [string] $BuildArch
    )

    # Look for Visual Studio/Build Tools 2017 or later (version 15.0 or above)
    $VsInstallPath = Get-VSSetupInstance | `
        Select-VSSetupInstance -Product "*" -Version "15.0" -Latest | `
        Select-Object -ExpandProperty "InstallationPath"

    if ($VsInstallPath -Eq "") { $VsInstallPath = "<N/A>" }

    if ($BuildArch -Eq "x86_64")
    {
        $VcVarsBin = "$VsInstallPath\VC\Auxiliary\build\vcvars64.bat"
    }
    else
    {
        $VcVarsBin = "$VsInstallPath\VC\Auxiliary\build\vcvars32.bat"
    }

    ""
    "**********************************************************************"
    "Using Visual Studio/Build Tools environment settings located at"
    $VcVarsBin
    "**********************************************************************"
    ""

    if (-Not (Test-Path -Path $VcVarsBin))
    {
        Throw "Microsoft Visual Studio ($BuildArch variant) is not installed. " + `
            "Please install Visual Studio 2017 or above it before running this script."
    }

    # Import environment variables set by vcvarsXX.bat into current scope
    $EnvDump = [System.IO.Path]::GetTempFileName()
    Invoke-Native-Command -Command "cmd" `
        -Arguments ("/c", "`"$VcVarsBin`" && set > `"$EnvDump`"")

    foreach ($_ in Get-Content -Path $EnvDump)
    {
        if ($_ -Match "^([^=]+)=(.*)$")
        {
            Set-Item "Env:$($Matches[1])" $Matches[2]
        }
    }

    Remove-Item -Path $EnvDump -Force
}



# Build Windows installer
Function Build-Installer
{
    param(
        [string] $BuildOption
    )

    foreach ($_ in Get-Content -Path "$RootPath\$AppName.pro")
    {
        if ($_ -Match "^VERSION *= *(.*)$")
        {
            $AppVersion = $Matches[1]
            break
        }
    }

    if ($BuildOption -ne "")
    {
        Invoke-Native-Command -Command "$RootPath\libs\NSIS\NSIS-source\makensis" `
            -Arguments ("/v4", "/DAPP_NAME=$AppName", "/DAPP_VERSION=$AppVersion", `
            "/DROOT_PATH=$RootPath", "/DWINDOWS_PATH=$WindowsPath", "/DDEPLOY_PATH=$DeployPath", `
            "/DBUILD_OPTION=$BuildOption", `
            "$WindowsPath\installer.nsi")
    }
    else
    {
        Invoke-Native-Command -Command "$RootPath\libs\NSIS\NSIS-source\makensis" `
            -Arguments ("/v4", "/DAPP_NAME=$AppName", "/DAPP_VERSION=$AppVersion", `
            "/DROOT_PATH=$RootPath", "/DWINDOWS_PATH=$WindowsPath", "/DDEPLOY_PATH=$DeployPath", `
            "$WindowsPath\installer.nsi")
    }
}

# Build and copy NS-Process dll
Function Build-NSProcess
{
    if (!(Test-Path -path "$RootPath\libs\NSIS\nsProcess.dll")) {

        echo "Building nsProcess..."

        $OriginalEnv = Get-ChildItem Env:
        Initialize-Build-Environment -BuildArch "x86"

        Invoke-Native-Command -Command "msbuild" `
            -Arguments ("$RootPath\libs\NSIS\nsProcess\nsProcess.sln", '/p:Configuration="Release UNICODE"', `
            "/p:Platform=Win32")

        Move-Item -Path "$RootPath\libs\NSIS\nsProcess\Release\nsProcess.dll" -Destination "$RootPath\libs\NSIS\nsProcess.dll" -Force
        Remove-Item -Path "$RootPath\libs\NSIS\nsProcess\Release\" -Force -Recurse
        $OriginalEnv | % { Set-Item "Env:$($_.Name)" $_.Value }
    }
}

Clean-Build-Environment
Install-Dependencies

Build-NSProcess

