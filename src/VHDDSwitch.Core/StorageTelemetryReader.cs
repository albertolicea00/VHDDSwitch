using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

namespace VHDDSwitch.Core
{
    public interface IStorageTelemetryReader
    {
        List<StorageDisk> ReadDisks();
        StorageDisk ReadDetails(StorageDisk disk);
    }

    // Read-only UI telemetry. Mutations continue to use StorageService's full,
    // fresh discovery and validation; these partial snapshots never authorize I/O.
    public sealed class StorageTelemetryReader : IStorageTelemetryReader
    {
        const string Namespace = @"\\.\root\Microsoft\Windows\Storage";
        static readonly string[] Buses = { "Unknown", "SCSI", "ATAPI", "ATA", "1394", "SSA", "Fibre", "USB", "RAID", "iSCSI", "SAS", "SATA", "SD", "MMC", "Virtual", "FileBackedVirtual", "StorageSpaces", "NVMe", "SCM", "UFS" };

        static EnumerationOptions Options()
        { return new EnumerationOptions { ReturnImmediately = true, Rewindable = false, Timeout = TimeSpan.FromSeconds(12) }; }
        static string Text(ManagementBaseObject item, string property)
        { return Convert.ToString(item[property], CultureInfo.InvariantCulture) ?? ""; }
        static long Number(ManagementBaseObject item, string property)
        { return Convert.ToInt64(item[property] ?? 0, CultureInfo.InvariantCulture); }
        static bool Flag(ManagementBaseObject item, string property)
        { return Convert.ToBoolean(item[property] ?? false, CultureInfo.InvariantCulture); }
        static string Letter(ManagementBaseObject item)
        { char value = Convert.ToChar(item["DriveLetter"] ?? '\0', CultureInfo.InvariantCulture); return value == '\0' ? "" : value.ToString(); }
        static string Health(long value)
        { return value == 0 ? "Healthy" : value == 1 ? "Warning" : value == 2 ? "Unhealthy" : "Unknown"; }

        public List<StorageDisk> ReadDisks()
        {
            var result = new List<StorageDisk>();
            using (var search = new ManagementObjectSearcher(new ManagementScope(Namespace), new ObjectQuery("SELECT Number, UniqueId, FriendlyName, SerialNumber, BusType, IsOffline, IsBoot, IsSystem, Size, PartitionStyle, HealthStatus FROM MSFT_Disk"), Options()))
            using (var rows = search.Get())
                foreach (ManagementObject item in rows)
                    using (item)
                    {
                        long bus = Number(item, "BusType"), style = Number(item, "PartitionStyle");
                        bool offline = Flag(item, "IsOffline");
                        result.Add(new StorageDisk {
                            Number = (int)Number(item, "Number"), UniqueId = Text(item, "UniqueId"), FriendlyName = Text(item, "FriendlyName"), SerialNumber = Text(item, "SerialNumber"),
                            BusType = bus >= 0 && bus < Buses.Length ? Buses[bus] : "Unknown", IsOffline = offline, Protected = Flag(item, "IsBoot") || Flag(item, "IsSystem"),
                            Size = Number(item, "Size"), PartitionStyle = style == 1 ? "MBR" : style == 2 ? "GPT" : "RAW", HealthStatus = Health(Number(item, "HealthStatus")),
                            OperationalStatus = offline ? "Offline" : "Online", HasTelemetry = false, TelemetryState = TelemetryState.Loading
                        });
                    }
            return result;
        }

        public StorageDisk ReadDetails(StorageDisk disk)
        {
            var result = new StorageDisk { Number = disk.Number, UniqueId = disk.UniqueId, SerialNumber = disk.SerialNumber, Protected = disk.Protected };
            // Offline disks have no live filesystem telemetry to wait for.
            if (disk.IsOffline) return result;
            using (var search = new ManagementObjectSearcher(new ManagementScope(Namespace), new ObjectQuery("SELECT * FROM MSFT_Partition WHERE DiskNumber = " + disk.Number.ToString(CultureInfo.InvariantCulture)), Options()))
            using (var partitions = search.Get())
                foreach (ManagementObject partition in partitions)
                    using (partition)
                    {
                        string letter = Letter(partition);
                        result.Protected |= Flag(partition, "IsBoot") || Flag(partition, "IsSystem") || letter.Equals("C", StringComparison.OrdinalIgnoreCase) || (letter + ":").Equals(Environment.GetEnvironmentVariable("SystemDrive"), StringComparison.OrdinalIgnoreCase);
                        using (var volumes = partition.GetRelated("MSFT_Volume", "MSFT_PartitionToVolume", null, null, null, null, false, Options()))
                            foreach (ManagementObject volume in volumes)
                                using (volume)
                                    result.Volumes.Add(new StorageVolume { Path = Text(volume, "Path"), DriveLetter = Letter(volume), Label = Text(volume, "FileSystemLabel"), FileSystem = Text(volume, "FileSystem"), TotalBytes = Number(volume, "Size"), FreeBytes = Number(volume, "SizeRemaining") });
                    }
            return result;
        }
    }
}
