@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo ERROR: .NET Framework compiler not found.
  exit /b 1
)
if not exist build mkdir build
if not exist dist mkdir dist
set "OUTPUT=dist\VHDDSwitch.exe"
if not "%~1"=="" set "OUTPUT=%~1"
for %%F in ("%OUTPUT%") do set "OUTPUTDIR=%%~dpF"
if not exist "%OUTPUTDIR%" mkdir "%OUTPUTDIR%"
"%CSC%" /nologo /target:library /optimize+ /out:build\VHDDSwitch.Core.dll /r:System.Web.Extensions.dll /r:System.Management.dll /resource:src\VHDDSwitch.Core\Storage.ps1,Storage.ps1 src\VHDDSwitch.Core\*.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%OUTPUT%" /win32manifest:src\VHDDSwitch.Desktop\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll /resource:src\VHDDSwitch.Core\Storage.ps1,Storage.ps1 src\VHDDSwitch.Core\*.cs src\VHDDSwitch.Desktop\*.cs
if errorlevel 1 exit /b 1
if not exist "%OUTPUTDIR%settings.json" copy /y settings.json "%OUTPUTDIR%settings.json" >nul
copy /y settings.schema.json "%OUTPUTDIR%settings.schema.json" >nul
echo Build OK: %OUTPUT%
exit /b 0
