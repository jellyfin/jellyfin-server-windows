/*  FindProcess.nsh
*   https://nsis.sourceforge.io/FindProcess
*   written by Donald Miller
*   Mar 7, 2007
*
*/

!include LogicLib.nsh
!include WordFunc.nsh
!insertmacro WordFind

!ifndef FindProcess
!define FindProcess         '!insertmacro FindProcess'

!macro FindProcess ProcessList BoolReturn
    Push '${ProcessList}'
    Call FindProcess
    Pop ${BoolReturn}
!macroend

Function FindProcess
  # return True if any process in ProcessList is active
  Exch $0     ; get ProcessList, save $0
  Push $1
  Push $2
  Push $R0
  Push $R1
  Push $R2

  StrCpy $2 "$0,"                 ; $2 = ProcessList

  Push 0                          ; set return value = False

  # method based upon one by Phoenix1701@gmail.com  1/27/07

  System::Alloc 1024
  Pop $R0                         ; process list buffer

  # get an array of all process ids
  System::Call "Psapi::EnumProcesses(i R0, i 1024, *i .R1)i .r0"
  ${Unless} $0 = 0

      IntOp $R1 $R1 / 4           ; Divide by sizeof(DWORD) to get $R1 process count
      IntOp $R1 $R1 - 1           ; decr for 0 base loop

      ClearErrors
      ${For} $R2 0 $R1
          # get a PID from the array
          IntOp $0 $R2 << 2
          IntOp $0 $0 + $R0           ; buffer.dword[i]
          System::Call "*$0(i .r0)"   ; Get next PID

          ${Unless} $0 = 0
              Push $0
              Call GetProcessName
              Pop $1

              # is this process one we are looking for?
              ${WordFind} '$2' ',' 'E/$1' $0
              ${Unless} ${Errors}
                  # yes, change return value
                  Pop $0          ; discard old result
                  Push 1          ; set return True

                  # exit the loop
                  ${Break}
              ${EndUnless}
          ${EndUnless}
      ${Next}

  ${EndUnless}

  System::Free $R0

  Pop $0              ; get return value
  Pop $R2             ; restore registers
  Pop $R1
  Pop $R0
  Pop $2
  Pop $1
  Exch $0
FunctionEnd

Function GetProcessName
  # ( Pid -- ProcessName )
  Exch $2                     ; get Pid, save $2
  Push $0
  Push $1
  Push $3
  Push $R0

  System::Call "Kernel32::OpenProcess(i 1040, i 0, i r2)i .r3"

  StrCpy $2 "<unknown>"       ; set return value

  ${Unless} $3 = 0            ; $3 is hProcess
      # get hMod array
      System::Alloc 1024
      Pop $R0

      # params: Pid, &hMod, sizeof(hMod), &cb
      System::Call "Psapi::EnumProcessModules(i r3, i R0, i 1024, *i .r1)i .r0"

      ${Unless} $0 = 0
          # get first hMod
          System::Call "*$R0(i .r0)"

          # get BaseName; params: Pid, hMod, szBuffer, sizeof(szBuffer)
          System::Call "Psapi::GetModuleBaseName(i r3, i r0, t .r2, i 256)i .r0"
      ${EndUnless}

      System::Free $R0
      System::Call "kernel32::CloseHandle(i r3)"
  ${EndUnless}

  Pop $R0                     ; restore registers
  Pop $3
  Pop $1
  Pop $0
  Exch $2                     ; save process name
FunctionEnd
!endif
