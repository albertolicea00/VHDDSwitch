@echo off
setlocal
if not "%~1"=="" goto command
:menu
echo VHDDSwitch: [1] Montar  [2] Desmontar  [3] Estado  [4] Salir
choice /c 1234 /n
if errorlevel 4 exit /b 0
if errorlevel 3 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VHDDSwitch.ps1" -Action status
) else if errorlevel 2 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VHDDSwitch.ps1" -Action eject
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VHDDSwitch.ps1" -Action mount
)
goto menu
:command
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VHDDSwitch.ps1" %*
exit /b %errorlevel%
