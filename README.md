# VHDDSwitch

Native Windows external storage manager with a Windows 11 styled dashboard.

See [documentation](docs/README.md) for setup, configuration, operation and safety limits.

```cmd
build.bat dist\Windows11\VHDDSwitch.exe
scripts\Preview-UI.bat
```

The preview uses simulated data and never queries or modifies real disks. Run the executable without `--demo` for normal operation.

The dashboard discovers disk identities first through read-only WMI, then loads volume details with two concurrent workers. Skeletons indicate pending information. Existing details remain visible during refreshes. Disk mutations still require fresh validation through the storage engine.

Generated executables and screenshots are local build artifacts and must not be committed.
