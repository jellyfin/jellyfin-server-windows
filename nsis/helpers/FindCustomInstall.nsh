; Detect a *running* Jellyfin Server that has no registry record
; (portable installs, hand-moved install folders, servers launched from a
; scheduled task / batch file with a custom --datadir).
;
; Without this, such a server is invisible to the installer: the user ends up
; with a second install beside the old one, pointed at a different (empty)
; data folder, and it looks like the library was lost.
;
; After calling DetectRunningCustomServer:
;   $_CUSTOMINST_     install directory of the running server ("" if not found)
;   $_CUSTOMDATADIR_  its --datadir value ("" if none / not detected)
;   $_CUSTOMCMD_      its full command line ("" if not detected)

Var _CUSTOMINST_
Var _CUSTOMDATADIR_
Var _CUSTOMCMD_
Var _CUSTOMADOPTED_

!define JF_PROBE_FILE '$TEMP\jellyfin-install-probe.txt'

; Removes the trailing CRLF FileRead leaves on a line.
; Usage:  Push $R5
;         Call TrimCRLF
;         Pop $R5
Function TrimCRLF
    Exch $R9
    Push $8
    StrLen $8 $R9
    ${If} $8 > 1
        StrCpy $R9 $R9 -2 ; probe lines are always CRLF-terminated
    ${EndIf}
    Pop $8
    Exch $R9
FunctionEnd

Function DetectRunningCustomServer
    StrCpy $_CUSTOMINST_ ""
    StrCpy $_CUSTOMDATADIR_ ""
    StrCpy $_CUSTOMCMD_ ""
    StrCpy $_CUSTOMADOPTED_ "No"

    ; Ask WMI (via PowerShell) about the running process: works no matter who
    ; started it (service, task scheduler, batch file, console). All parsing
    ; happens there so the NSIS side only reads three plain lines:
    ;   line 1 = install directory   line 2 = --datadir   line 3 = command line
    Delete `${JF_PROBE_FILE}`
    nsExec::ExecToLog `powershell -NoProfile -ExecutionPolicy Bypass -Command "$$p = Get-CimInstance Win32_Process -Filter \"name = 'jellyfin.exe'\" | Select-Object -First 1; if ($$p) { $$d = if ($$p.CommandLine -match '--datadir\s+\x22?([^\x22]+?)\x22?(\s+--|\s*$$)') { $$matches[1] } else { '' }; [IO.File]::WriteAllLines('$TEMP\jellyfin-install-probe.txt', @((Split-Path -Parent $$p.ExecutablePath), $$d, $$p.CommandLine)) }"`
    Pop $0 ; exit code; an absent probe file simply means "nothing was running"

    ClearErrors
    FileOpen $9 `${JF_PROBE_FILE}` r
    IfErrors done
        FileRead $9 $R5            ; install dir
        IfErrors close
        Push $R5
        Call TrimCRLF
        Pop $R5
        FileRead $9 $R6            ; --datadir
        IfErrors +3
        Push $R6
        Call TrimCRLF
        Pop $R6
        StrCpy $_CUSTOMDATADIR_ $R6
        FileRead $9 $R7            ; command line
        IfErrors +3
        Push $R7
        Call TrimCRLF
        Pop $R7
        StrCpy $_CUSTOMCMD_ $R7
        ${If} ${FileExists} "$R5\jellyfin.exe"
            StrCpy $_CUSTOMINST_ $R5
        ${EndIf}
    close:
        FileClose $9
        Delete `${JF_PROBE_FILE}`
    done:
FunctionEnd
