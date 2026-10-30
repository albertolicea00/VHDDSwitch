using System;
using VHDDSwitch.Core;
class CoreTests
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static bool Rejected(StorageDisk disk) { try { StorageService.ValidateTarget(disk); return false; } catch (InvalidOperationException) { return true; } }
    static int Main()
    {
        var disk = new StorageDisk { UniqueId = "test", FriendlyName = "JMicron USB", BusType = "USB" };
        var profile = new EnclosureProfile { hardwareFilter = "*jmicron*" };
        Check(profile.Matches(disk), "case insensitive wildcard");
        profile.hardwareFilter = "*TOSHIBA*"; Check(!profile.Matches(disk), "profile isolation");
        profile.hardwareFilter = "JMicron US?"; Check(profile.Matches(disk), "single wildcard");
        StorageService.ValidateTarget(disk); checks++;
        disk.Protected = true; Check(Rejected(disk), "system disk");
        disk.Protected = false; disk.BusType = "SATA"; Check(Rejected(disk), "internal SATA");
        disk.BusType = "NVMe"; Check(Rejected(disk), "internal NVMe");
        disk.BusType = "USB"; disk.UniqueId = ""; Check(Rejected(disk), "unknown identity");
        var settings = Settings.Load("settings.json"); Check(settings.profiles.Count == 2, "settings");
        Check(settings.profiles[0].referenceLetters.Length == 5, "five bays");
        Check(StorageService.RunPowerShell("Write-Output 'transport-ok'") == "transport-ok", "PowerShell transport");
        bool failed = false; try { StorageService.RunPowerShell("throw 'expected-error'"); } catch (System.IO.IOException) { failed = true; }
        Check(failed, "PowerShell errors propagate");
        Console.WriteLine(checks + " checks passed; no disk mutations."); return 0;
    }
}
