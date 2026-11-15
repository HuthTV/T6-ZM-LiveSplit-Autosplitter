using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LiveSplit.T6ZM
{
    internal static class GameBuild
    {
        const uint QueryLimited = 0x1000;
        const string CompFolder = "Plutonium-oldstable";

        public static string Name(uint processId)
        {
            IntPtr handle = OpenProcess(QueryLimited, false, processId);
            if (handle == IntPtr.Zero) return "T6ZM";
            try
            {
                var path = new StringBuilder(1024);
                int size = path.Capacity;
                string image = QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : "";
                string exe = Path.GetFileName(image);
                if (exe.Equals("plutonium-bootstrapper-win32.exe", StringComparison.OrdinalIgnoreCase))
                    return image.IndexOf("\\" + CompFolder + "\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "Plutonium (comp)" : "Plutonium";
                if (exe.Equals("t6zm.exe", StringComparison.OrdinalIgnoreCase))
                    return Environment.Is64BitOperatingSystem && IsWow64Process(handle, out bool wow) && !wow ? "W Store" : "Steam";
                return "T6ZM";
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        [DllImport("kernel32", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("kernel32", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32", SetLastError = true)] static extern bool IsWow64Process(IntPtr handle, out bool wow64);
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryFullProcessImageName(IntPtr handle, int flags, StringBuilder path, ref int size);
    }
}
