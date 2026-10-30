@echo off
setlocal
cd /d "%~dp0.."
call build.bat "%~1"
if errorlevel 1 exit /b 1
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:build\CoreTests.exe /r:build\VHDDSwitch.Core.dll tests\CoreTests.cs
if errorlevel 1 exit /b 1
build\CoreTests.exe
exit /b %errorlevel%
