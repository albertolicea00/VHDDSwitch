@echo off
setlocal
if exist "%~dp0..\dist\Windows11\VHDDSwitch.exe" goto preview
call "%~dp0..\build.bat" dist\Windows11\VHDDSwitch.exe
if errorlevel 1 exit /b 1
:preview
start "" "%~dp0..\dist\Windows11\VHDDSwitch.exe" --demo
