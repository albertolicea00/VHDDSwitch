@echo off
setlocal
cd /d "%~dp0.."
if not exist build mkdir build
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /main:DesktopPreview /optimize+ /out:build\VHDDSwitch.Preview.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll /resource:src\VHDDSwitch.Core\Storage.ps1,Storage.ps1 src\VHDDSwitch.Core\*.cs src\VHDDSwitch.Desktop\*.cs tests\DesktopPreview.cs
exit /b %errorlevel%
