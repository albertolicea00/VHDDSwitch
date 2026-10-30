using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VHDDSwitch.Core
{
    public enum TelemetryState { Ready, Loading, Error }
    public sealed class StorageVolume
    {
        public string Path { get; set; }
        public string DriveLetter { get; set; }
        public string Label { get; set; }
        public string FileSystem { get; set; }
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
    }
    public sealed class StorageDisk
    {
        public int Number { get; set; }
        public string UniqueId { get; set; }
        public string FriendlyName { get; set; }
        public string SerialNumber { get; set; }
        public string BusType { get; set; }
        public string OperationalStatus { get; set; }
        public string HealthStatus { get; set; }
        public string PartitionStyle { get; set; }
        public bool IsOffline { get; set; }
        public bool Protected { get; set; }
        public long Size { get; set; }
        public List<StorageVolume> Volumes { get; set; }
        public TelemetryState TelemetryState { get; set; }
        public bool HasTelemetry { get; set; }
        public string TelemetryError { get; set; }
        public StorageDisk() { Volumes = new List<StorageVolume>(); HasTelemetry = true; }
    }
    public sealed class EnclosureProfile
    {
        public string id { get; set; }
        public string name { get; set; }
        public string hardwareFilter { get; set; }
        public int expectedBays { get; set; }
        public string[] referenceLetters { get; set; }
        public bool autoMountOnDetect { get; set; }
        public override string ToString() { return name; }
        public bool Matches(StorageDisk disk)
        {
            return Regex.IsMatch(disk.FriendlyName ?? "", "^" + Regex.Escape(hardwareFilter).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }
    }
    public sealed class AppSettings
    {
        public string theme { get; set; }
        public int refreshIntervalSeconds { get; set; }
        public bool minimizeToTray { get; set; }
        public bool requireAdminOnStartup { get; set; }
    }
    public sealed class Settings
    {
        public AppSettings app { get; set; }
        public List<EnclosureProfile> profiles { get; set; }
        public static Settings Load(string path)
        {
            var result = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path));
            if (result == null || result.app == null || result.profiles == null || result.profiles.Count == 0)
                throw new InvalidDataException("settings.json requires app settings and at least one profile.");
            if (result.app.refreshIntervalSeconds < 2 || result.app.refreshIntervalSeconds > 3600)
                throw new InvalidDataException("refreshIntervalSeconds must be between 2 and 3600.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (result.app.theme != "Dark" || !result.app.requireAdminOnStartup)
                throw new InvalidDataException("This version requires theme=Dark and requireAdminOnStartup=true.");
            foreach (var p in result.profiles)
            {
                if (String.IsNullOrWhiteSpace(p.id) || !ids.Add(p.id) || String.IsNullOrWhiteSpace(p.name) || String.IsNullOrWhiteSpace(p.hardwareFilter) || p.expectedBays < 1 || p.expectedBays > 64)
                    throw new InvalidDataException("Invalid profile or duplicate ID.");
            }
            return result;
        }
    }
}
