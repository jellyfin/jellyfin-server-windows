param (
    [string] $BuildOption = ""
)

# Fail early on all errors
$ErrorActionPreference = "Stop"

# Invoke-WebRequest is really slow by default because it renders a progress bar.
# Disabling this, improves vastly performance:
$ProgressPreference = 'SilentlyContinue'

# Global constants
$RootPath = "$PWD"


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


# Install VSSetup (Visual Studio detection)
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


# Build and copy NS-Process dll
Function Build-NSProcess
{
    if (!(Test-Path -path "$RootPath\..\..\nsis\plugins\nsProcess.dll")) {

        echo "Building nsProcess..."

        $OriginalEnv = Get-ChildItem Env:
        Initialize-Build-Environment -BuildArch "x86"

        Invoke-Native-Command -Command "msbuild" `
            -Arguments ("$RootPath\nsProcess.sln", '/p:Configuration="Release UNICODE"', `
            "/p:Platform=Win32")

        Move-Item -Path "$RootPath\Release\nsProcess.dll" -Destination "$RootPath\..\..\nsis\plugins\nsProcess.dll" -Force
        Remove-Item -Path "$RootPath\Release\" -Force -Recurse
        $OriginalEnv | % { Set-Item "Env:$($_.Name)" $_.Value }
    }
}

Install-Dependencies
Build-NSProcess

