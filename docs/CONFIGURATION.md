# Configuration

Edit settings.json beside the executable while the application is closed. The root settings.json is the build template; existing output settings are preserved.

| Setting | Meaning |
| --- | --- |
| theme | Choose System, Light or Dark. |
| refreshIntervalSeconds | Refresh interval, from 2 to 3600 seconds. |
| minimizeToTray | Hide the normal window when minimized or closed. |
| requireAdminOnStartup | Must be true to match the UAC manifest. |

Each profile requires a unique id, name, hardwareFilter and expectedBays (1�64). The filter matches disk FriendlyName case-insensitively, supporting * and ?. It does not identify the parent USB controller. Multiple enclosures with identical names can match the same profile.

referenceLetters are informational and never select mutation targets. autoMountOnDetect defaults to false. When enabled, it attempts to bring newly detected offline disks online after the initial discovery.

The UI uses Segoe UI fonts, Fluent icons, dark native window chrome and rounded corners where supported. Content uses custom Windows Forms controls, not WinUI or Mica. Restart after changing display scaling.

Groups created in the UI store a members array of stable disk identity strings. allExternal marks the built-in external-drive view. hardwareFilter remains supported for legacy wildcard profiles but is not needed for manually selected groups. Opening the group editor converts edited legacy membership to an explicit disk selection when saved; review the checked disks before saving. Old output configurations are preserved by the build; a generic All external drives view is added automatically when loading them.
