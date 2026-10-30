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
                if (!process.WaitForExit(45000)) { process.Kill(); process.WaitForExit(); throw new TimeoutException("Almacenamiento: tiempo agotado; compruebe el estado antes de reintentar."); }
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
            if (disk.Protected) throw new InvalidOperationException("Disco del sistema/arranque protegido.");
            if (disk.BusType != "USB" && disk.BusType != "1394" && disk.BusType != "SD" && disk.BusType != "MMC")
                throw new InvalidOperationException("Se rechaza un disco sin bus externo reconocido: " + disk.BusType);
            if (String.IsNullOrWhiteSpace(disk.UniqueId)) throw new InvalidOperationException("Disco sin identidad estable; operacion rechazada.");
        }
        public void Change(StorageDisk selected, bool online, Action<string> log)
        {
            // Never trust the disk number retained by the UI after USB re-enumeration.
            var matches = Discover().Where(d => d.UniqueId == selected.UniqueId && d.SerialNumber == selected.SerialNumber).ToList();
            if (matches.Count != 1) throw new IOException("Identidad ausente o ambigua; actualice la lista.");
            var disk = matches[0]; ValidateTarget(disk);
            if (disk.IsOffline == !online) { log("Disco " + disk.Number + ": ya tiene el estado solicitado."); return; }
            var handles = new List<SafeFileHandle>();
            try
            {
                // Lock ALL volumes before dismounting any. A busy/unknown volume aborts.
                if (!online)
                {
                    if (disk.Volumes.Count == 0 && disk.PartitionStyle != "RAW") throw new IOException("No se pudieron verificar los volumenes del disco.");
                    foreach (var volume in disk.Volumes)
                    {
                        if (String.IsNullOrEmpty(volume.Path) || !volume.Path.StartsWith(@"\\?\Volume{")) throw new IOException("Volumen sin ruta GUID verificable.");
                        var h = Native.CreateFile(volume.Path.TrimEnd('\\'), 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                        handles.Add(h);
                        if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir " + volume.Path);
                        Native.Control(h, 0x90018, "Volumen ocupado: cierre las aplicaciones y reintente. " + volume.DriveLetter);
                        if (!Native.FlushFileBuffers(h)) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo vaciar la cache.");
                    }
                    foreach (var h in handles) Native.Control(h, 0x90020, "No se pudo desmontar el volumen.");
                }
                // Revalidate identity AND system flags inside the mutation process.
                string uid = Convert.ToBase64String(Encoding.UTF8.GetBytes(disk.UniqueId));
                string script = "$ErrorActionPreference='Stop'; $uid=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + uid + "')); $d=@(Get-Disk | Where-Object UniqueId -eq $uid); if($d.Count -ne 1 -or $d[0].Number -ne " + disk.Number + "){throw 'La topologia cambio'}; $d=$d[0]; $p=@($d|Get-Partition); if($d.IsBoot -or $d.IsSystem -or @($p|Where-Object {$_.IsBoot -or $_.IsSystem -or $_.DriveLetter -eq 'C' -or ($_.DriveLetter -and ($_.DriveLetter.ToString()+':') -eq $env:SystemDrive)}).Count -gt 0){throw 'Disco protegido'}; ";
                script += online ? "$d | Set-Disk -IsOffline $false; $d | Set-Disk -IsReadOnly $false; Update-HostStorageCache;" : "$d | Set-Disk -IsOffline $true;";
                script += " if((Get-Disk -Number " + disk.Number + ").IsOffline -ne $" + (!online ? "true" : "false") + "){throw 'Estado final no confirmado'}";
                RunPowerShell(script);
                log("Disco " + disk.Number + (online ? ": ONLINE confirmado." : ": cache vaciada, volumenes desmontados, OFFLINE confirmado. No implica apagado fisico."));
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
