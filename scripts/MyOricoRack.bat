@echo off
rem Compatibility entry point for the original rack utility.
call "%~dp0VHDDSwitch.bat" %*
exit /b %errorlevel%
