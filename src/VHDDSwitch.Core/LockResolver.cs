using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace VHDDSwitch.Core
{
    public static class LockResolver
    {
        [StructLayout(LayoutKind.Sequential)] public struct UniqueProcess { public int ProcessId; public System.Runtime.InteropServices.ComTypes.FILETIME StartTime; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LockingProcess
        {
            public UniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Service;
            public uint Type, Status, Session;
            [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
        }
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] static extern int RmStartSession(out uint session, int flags, StringBuilder key);
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] static extern int RmRegisterResources(uint session, uint files, string[] names, uint apps, IntPtr processes, uint services, IntPtr serviceNames);
        [DllImport("rstrtmgr.dll")] static extern int RmGetList(uint session, out uint needed, ref uint count, [In, Out] LockingProcess[] processes, ref uint reasons);
        [DllImport("rstrtmgr.dll")] static extern int RmEndSession(uint session);
        public static LockingProcess[] Find(string file)
        {
            uint session; int error = RmStartSession(out session, 0, new StringBuilder(33));
            if (error != 0) throw new Win32Exception(error);
            try
            {
                error = RmRegisterResources(session, 1, new[] { file }, 0, IntPtr.Zero, 0, IntPtr.Zero);
                if (error != 0) throw new Win32Exception(error);
                for (int retry = 0; retry < 4; retry++)
                {
                    uint needed, count = 0, reasons = 0;
                    error = RmGetList(session, out needed, ref count, null, ref reasons);
                    if (error == 0) return new LockingProcess[0];
                    if (error != 234) throw new Win32Exception(error);
                    var result = new LockingProcess[needed]; count = needed;
                    error = RmGetList(session, out needed, ref count, result, ref reasons);
                    if (error == 234) continue;
                    if (error != 0) throw new Win32Exception(error);
                    Array.Resize(ref result, (int)count); return result;
                }
                throw new InvalidOperationException("La lista de procesos cambio; reintente.");
            }
            finally { RmEndSession(session); }
        }
    }
}
