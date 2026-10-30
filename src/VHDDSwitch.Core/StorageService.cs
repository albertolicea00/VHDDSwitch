using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace VHDDSwitch.Core
{
    public sealed class StorageService
    {
        public static string RunPowerShell(string script)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + script)));
            start.UseShellExecute = false; start.CreateNoWindow = true;
            start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8; start.StandardErrorEncoding = Encoding.UTF8;
            using (var process = Process.Start(start))
            {
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(45000)) { process.Kill(); process.WaitForExit(); throw new TimeoutException("Storage operation timed out; verify disk state before retrying."); }
                if (process.ExitCode != 0) throw new IOException(stderr.Result.Trim());
                return stdout.Result.Trim();
            }
        }
        public List<StorageDisk> Discover()
        {
            using (var stream = typeof(StorageService).Assembly.GetManifestResourceStream("Storage.ps1"))
            using (var reader = new StreamReader(stream))
                return new JavaScriptSerializer().Deserialize<List<StorageDisk>>(RunPowerShell(reader.ReadToEnd()));
        }
        public static void ValidateTarget(StorageDisk disk)
        {
            if (disk.Protected) throw new InvalidOperationException("System or boot disk is protected.");
            if (disk.BusType != "USB" && disk.BusType != "1394" && disk.BusType != "SD" && disk.BusType != "MMC")
                throw new InvalidOperationException("Unsupported external bus: " + disk.BusType);
            if (String.IsNullOrWhiteSpace(disk.UniqueId)) throw new InvalidOperationException("Disk has no stable identity; operation rejected.");
        }
        public void EditVolume(StorageDisk selected, StorageVolume volume, string label, string letter)
        {
            var matches = Discover().Where(d => d.UniqueId == selected.UniqueId && d.SerialNumber == selected.SerialNumber).ToList();
            if (matches.Count != 1) throw new IOException("Disk identity is missing or ambiguous.");
            var disk = matches[0]; ValidateTarget(disk);
            if (disk.IsOffline || !disk.Volumes.Any(v => v.Path == volume.Path)) throw new IOException("Volume identity changed or disk is offline.");
            if (letter != null && (letter.Length != 1 || letter[0] < 'D' || letter[0] > 'Z')) throw new ArgumentException("Choose one drive letter from D to Z.");
            var payload = new JavaScriptSerializer().Serialize(new { uid = disk.UniqueId, path = volume.Path, label = label, letter = letter });
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
            string script = "$ErrorActionPreference='Stop'; $x=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "'))|ConvertFrom-Json; $d=@(Get-Disk|Where-Object UniqueId -eq $x.uid); if($d.Count -ne 1 -or $d[0].IsBoot -or $d[0].IsSystem){throw 'Disk identity or protection changed'}; $parts=@($d[0]|Get-Partition); if(@($parts|Where-Object {$_.IsBoot -or $_.IsSystem -or $_.DriveLetter -eq 'C'}).Count){throw 'Protected disk'}; $p=@($parts|Where-Object { @($_|Get-Volume|Where-Object Path -eq $x.path).Count -eq 1 }); if($p.Count -ne 1){throw 'Volume identity changed'}; ";
            script += letter != null ? "if(Get-PSDrive -Name $x.letter -ErrorAction SilentlyContinue){throw 'Drive letter already in use'}; $p[0]|Set-Partition -NewDriveLetter ([char]$x.letter); if((Get-Partition -DiskNumber $p[0].DiskNumber -PartitionNumber $p[0].PartitionNumber).DriveLetter -ne [char]$x.letter){throw 'Drive letter was not confirmed'}" : "$p[0]|Get-Volume|Set-Volume -NewFileSystemLabel $x.label; if(($p[0]|Get-Volume).FileSystemLabel -ne $x.label){throw 'Volume label was not confirmed'}";
            RunPowerShell(script);
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle file, IntPtr buffer, uint count, out uint read, IntPtr overlapped);
        public void KeepActive(StorageDisk selected)
        {
            var matches = Discover().Where(d => d.UniqueId == selected.UniqueId && d.SerialNumber == selected.SerialNumber).ToList();
            if (matches.Count != 1) throw new IOException("Disk identity changed.");
            var disk = matches[0]; ValidateTarget(disk); if (disk.IsOffline) return;
            using (var handle = Native.CreateFile(@"\\.\PhysicalDrive" + disk.Number, 0x80000000, 3, IntPtr.Zero, 3, 0x20000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr allocation = Marshal.AllocHGlobal(131072);
                try
                {
                    IntPtr aligned = new IntPtr((allocation.ToInt64() + 65535) & ~65535L); uint read;
                    if (!ReadFile(handle, aligned, 65536, out read, IntPtr.Zero) || read == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { Marshal.FreeHGlobal(allocation); }
            }
        }
        public void Change(StorageDisk selected, bool online, Action<string> log)
        {
            // Never trust the disk number retained by the UI after USB re-enumeration.
            var matches = Discover().Where(d => d.UniqueId == selected.UniqueId && d.SerialNumber == selected.SerialNumber).ToList();
            if (matches.Count != 1) throw new IOException("Missing or ambiguous disk identity; refresh the list.");
            var disk = matches[0]; ValidateTarget(disk);
            if (disk.IsOffline == !online) { log("Disk " + disk.Number + ": already in the requested state."); return; }
            var handles = new List<SafeFileHandle>();
            try
            {
                // Lock ALL volumes before dismounting any. A busy/unknown volume aborts.
                if (!online)
                {
                    if (disk.Volumes.Count == 0 && disk.PartitionStyle != "RAW") throw new IOException("Unable to verify disk volumes.");
                    foreach (var volume in disk.Volumes)
                    {
                        if (String.IsNullOrEmpty(volume.Path) || !volume.Path.StartsWith(@"\\?\Volume{")) throw new IOException("Volume has no verifiable GUID path.");
                        var h = Native.CreateFile(volume.Path.TrimEnd('\\'), 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                        handles.Add(h);
                        if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open " + volume.Path);
                        Native.Control(h, 0x90018, "Volume is busy; close applications and retry. " + volume.DriveLetter);
                        if (!Native.FlushFileBuffers(h)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to flush the cache.");
                    }
                    foreach (var h in handles) Native.Control(h, 0x90020, "Unable to dismount the volume.");
                }
                // Revalidate identity AND system flags inside the mutation process.
                string uid = Convert.ToBase64String(Encoding.UTF8.GetBytes(disk.UniqueId));
                string script = "$ErrorActionPreference='Stop'; $uid=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + uid + "')); $d=@(Get-Disk | Where-Object UniqueId -eq $uid); if($d.Count -ne 1 -or $d[0].Number -ne " + disk.Number + "){throw 'Disk topology changed'}; $d=$d[0]; $p=@($d|Get-Partition); if($d.IsBoot -or $d.IsSystem -or @($p|Where-Object {$_.IsBoot -or $_.IsSystem -or $_.DriveLetter -eq 'C' -or ($_.DriveLetter -and ($_.DriveLetter.ToString()+':') -eq $env:SystemDrive)}).Count -gt 0){throw 'Protected disk'}; ";
                script += online ? "$d | Set-Disk -IsOffline $false; $d | Set-Disk -IsReadOnly $false; Update-HostStorageCache;" : "$d | Set-Disk -IsOffline $true;";
                script += " if((Get-Disk -Number " + disk.Number + ").IsOffline -ne $" + (!online ? "true" : "false") + "){throw 'Final state not confirmed'}";
                RunPowerShell(script);
                log("Disk " + disk.Number + (online ? ": ONLINE confirmed." : ": cache flushed, volumes dismounted, OFFLINE confirmed. This does not imply hardware power-off."));
            }
            finally { foreach (var h in handles) h.Dispose(); }
        }
    }
    internal static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlushFileBuffers(SafeFileHandle handle);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
        internal static void Control(SafeFileHandle handle, uint code, string message)
        { uint returned; if (!DeviceIoControl(handle, code, IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), message); }
    }
}
