# Build and usage

Only one normal instance runs per Windows user and session. Opening the application again restores the existing window, including when hidden in the tray. Demo mode has its own single-instance scope so it cannot accidentally activate a real-storage window. Windows may still request UAC before the second process can notify the first instance.

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

## Drive groups and USB suggestions

The default view is All external drives, with no vendor or drive-letter assumptions. Open Manage groups in the sidebar to create, rename or delete groups and check individual disks. Membership uses UniqueId plus serial number, not disk numbers or letters. Disconnected members remain listed until unchecked. Empty groups match nothing. Demo edits stay in memory; normal edits are saved atomically beside the executable. Existing wildcard profiles remain compatible.

Suggest USB groups proposes disks that share the same concrete USB device instance in the Windows PnP parent tree. Review each proposal before accepting, then Save changes. Vendor/model similarity alone is insufficient. Root hubs and host controllers are not grouping keys. Enclosures exposing separate USB device interfaces may produce no suggestion; use manual selection in that case. Recommendations never mount or dismount disks.

## Appearance and drive actions

Choose System, Light or Dark in the sidebar. System follows the Windows app theme, including changes while the app is running. The choice is saved beside the executable outside demo mode.

Open Actions on a disk card to mount/dismount that disk, keep it active, rename a selected filesystem volume or assign a free drive letter from D to Z. A disk may contain multiple volumes: select the intended volume in the submenu. Windows remembers letter assignments where possible; the app does not enforce letters across computers. Existing letters are never stolen from another drive. System and boot disk safeguards and fresh identity checks also apply to volume changes.

Mount all and Dismount all act on the selected group. Keep group active toggles currently online, unprotected members of that group. Individual keep-active selections are available in each card menu. Selections last only for the running session and stop before dismounting through the app. Each interval attempts an uncached, read-only physical disk read after revalidating identity. It does not write keep-alive files or change system-wide power settings. Enclosure firmware may ignore these attempts; continuous spindle rotation or head position cannot be guaranteed. Native device I/O may wait for the driver. Keep-active errors disable that disk's selection and appear in the log.

Demo mode simulates labels, letters and disk state and never performs keep-active reads. New storage operations remain untested against real drives until suitable test hardware is available.
