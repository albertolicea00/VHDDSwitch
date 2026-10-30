# Development and testing

Core contains models, full storage validation, native volume locking and Restart Manager integration. StorageTelemetryReader provides separate read-only WMI inventory and per-disk metadata queries. Desktop contains the dashboard, rendering controls and simulated data.

The dashboard publishes inventory before metadata completes, limits detail queries to two concurrent workers, preserves previous values during refresh and isolates per-disk failures. Pending data uses skeletons rather than zero capacity. Mutation buttons remain disabled during discovery and incomplete telemetry; mutation operations always perform independent full validation.

Build with csc.exe through build.bat. No external packages are required.

```cmd
tests\run.bat dist\Windows11\VHDDSwitch.exe
tests\build-preview.bat
build\VHDDSwitch.Preview.exe --render
```

Tests use model data and simulated UI state. Rendering writes local PNG artifacts under build without querying real disks. Never commit generated binaries or screenshots.

System and boot disks are protected. Internal SATA/NVMe disks are rejected for mutations. All volumes must lock and flush before dismounting. Offline is a logical state and does not guarantee spindle shutdown or physical USB removal. Restart Manager checks individual files, not every handle on a volume. Native driver I/O has no guaranteed cancellation. Real mount/dismount validation remains pending until test drives are available.

The native WMI telemetry path has not been exercised against real drives during this change, following the requirement to avoid accessing drives currently in use.
