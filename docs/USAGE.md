# Build and usage

Requires Windows 10/11, .NET Framework 4.8 and Windows PowerShell 5.1 with the Storage module. No NuGet downloads are required.

From the repository root:

```cmd
build.bat dist\Windows11\VHDDSwitch.exe
scripts\Preview-UI.bat
```

The preview launcher always passes `--demo`. Mount, dismount and refresh only affect simulated data in this mode. The normal executable requests administrator access through UAC.

To use real storage, run `dist\Windows11\VHDDSwitch.exe` without arguments. Select a profile in the sidebar. Disk cards appear before filesystem details; skeletons indicate pending labels and capacity. Expand Details for model, serial and health information. View log shows operation results.

Normal mode queries real storage. Mount all and Dismount all require confirmation. Automatic mounting is disabled by default. Do not test storage operations on drives currently in use.

Closing or minimizing the normal window hides it when minimizeToTray is enabled. Use Exit in the tray menu to quit. Demo windows close directly.

The build preserves settings.json beside the selected output executable. Keep the executable, settings.json and settings.schema.json together. Close a running instance before rebuilding its executable, or select another output path.

The CLI loads dist/VHDDSwitch.exe and supports mount, eject, status and help with an optional hardware wildcard. It has no simulation mode. Status queries real storage; mount and eject modify disks and request UAC. Exit code 1 indicates failure.
