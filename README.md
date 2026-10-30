# VHDDSwitch 🎛️💾

**Universal External Storage & Multi-Bay Rack Controller for Windows**

VHDDSwitch is a lightweight, high-performance Windows storage utility designed to manage, monitor, mount, and safely disconnect external multi-bay drive enclosures (Orico, Sabrent, QNAP, Synology USB, TerraMaster), external hard drives, and SSD docks.

---

## 🌟 Key Features

- **⚡ Instant Mounting (Online / Offline Toggle):** Switch individual drives or entire multi-bay arrays between active `Online` and safe `Offline` states (heads safely parked, ready to power off or unplug without data loss).
- **🛡️ Safe Eject with Cache Flushing:** Automatically flushes filesystem write caches and ejects volumes cleanly via Windows Shell APIs before taking disks offline.
- **🔍 Dynamic Hardware Discovery:** Automatically discovers disks by controller chipset (e.g. `JMicron`, `ASMedia`, `Realtek`) or custom vendor patterns rather than relying on brittle, hardcoded disk numbers.
- **📊 Real-time Storage Telemetry:** Visualizes partition layout, drive letters, volume labels, health status, capacity, and remaining free space.
- **⚙️ Multi-Rack & Profile Support:** Configurable profiles for multi-bay enclosures (e.g. 5-bay racks), standalone backup drives, and future expansion docks.
- **🚀 Zero Bloat & Zero Runtime Dependencies:** Designed for native Windows execution with instantaneous startup and minimal footprint.

---

## 📁 Repository Structure

```text
VHDDSwitch/
├── .gitignore               # Windows, C#, and build artifacts exclusions
├── README.md                # Project documentation and guide
├── BUILD_PLAN.md            # Comprehensive architecture & build plan for the native C# app
├── scripts/
│   └── VHDDSwitch.bat       # Parameterized CLI/batch utility with auto-elevation
└── src/                     # (Coming in Phase 1) Native C# desktop application source
```

---

## 🚀 Quick Start: CLI Script (`scripts/VHDDSwitch.bat`)

The included `VHDDSwitch.bat` provides an immediate interactive menu and scriptable command-line interface with automatic Administrator elevation.

### Interactive Mode
Double-click `scripts\VHDDSwitch.bat` or run:
```cmd
scripts\VHDDSwitch.bat
```
This presents an interactive menu to Mount, Eject, check Status, or adjust hardware filters on the fly.

### Command-Line Arguments (Automation & Shortcuts)
```cmd
:: Mount all Orico (JMicron) disks
scripts\VHDDSwitch.bat mount

:: Safely eject all Orico disks (flush cache + offline)
scripts\VHDDSwitch.bat eject

:: Check status of disks and volumes
scripts\VHDDSwitch.bat status

:: Target a specific external drive or chipset
scripts\VHDDSwitch.bat mount *TOSHIBA*
scripts\VHDDSwitch.bat eject *ASMedia*
```

---

## 🗺️ Next Milestone: Native C# Desktop Application

We are developing the native Windows desktop edition of **VHDDSwitch** in C# featuring:
- Modern UI with tray icon support and quick toggle switches.
- Per-bay LED status indicators (🟢 Online / ⚪ Offline).
- Process lock detector (identifies which application is keeping a drive busy when eject fails).
- JSON-based configuration profiles (`settings.json`).

See [BUILD_PLAN.md](file:///C:/Users/albertolicea00/Develop/VHDDSwitch/BUILD_PLAN.md) for the detailed architectural blueprint.
