param([ValidateSet('VHDDSwitch.exe','VHDDSwitch.Win11.exe','Windows11\VHDDSwitch.exe')][string]$ExecutableName = 'VHDDSwitch.exe')
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\dist\' + $ExecutableName)))
if (-not (Test-Path -LiteralPath $target)) { throw 'Ejecute build.bat primero.' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'VHDDSwitch.lnk'))
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = Split-Path $target
$shortcut.Description = 'VHDDSwitch - Almacenamiento externo'
$shortcut.Save()
