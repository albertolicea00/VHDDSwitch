using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace VHDDSwitch.Core
{
    public static class UsbGrouping
    {
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint node, string id, uint flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint child, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint node, StringBuilder id, uint length, uint flags);
        public static string FindInterface(int number)
        {
            using (var search = new ManagementObjectSearcher(@"root\cimv2", "SELECT PNPDeviceID FROM Win32_DiskDrive WHERE Index = " + number))
            using (var rows = search.Get())
                foreach (ManagementObject row in rows)
                    using (row)
                    {
                        uint node;
                        if (CM_Locate_DevNodeW(out node, Convert.ToString(row["PNPDeviceID"]), 0) != 0) continue;
                        for (int depth = 0; depth < 24; depth++)
                        {
                            var id = new StringBuilder(1024);
                            if (CM_Get_Device_IDW(node, id, 1024, 0) != 0) break;
                            // Use a concrete USB device instance, never a root hub or host controller.
                            if (id.ToString().StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase)) return id.ToString();
                            uint parent; if (CM_Get_Parent(out parent, node, 0) != 0) break; node = parent;
                        }
                    }
            return null;
        }
        public static List<EnclosureProfile> Suggest(IEnumerable<StorageDisk> disks)
        {
            return disks.Where(d => d.BusType == "USB" && !d.Protected && !String.IsNullOrWhiteSpace(d.UniqueId) && !String.IsNullOrWhiteSpace(d.UsbInterfaceId))
                .GroupBy(d => d.UsbInterfaceId, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(EnclosureProfile.Identity).Distinct().Count() > 1)
                .Select((g, i) => new EnclosureProfile { id = Guid.NewGuid().ToString("N"), name = "USB group " + (i + 1), members = g.Select(EnclosureProfile.Identity).Distinct().ToArray(), expectedBays = g.Select(EnclosureProfile.Identity).Distinct().Count() }).ToList();
        }
    }
}
