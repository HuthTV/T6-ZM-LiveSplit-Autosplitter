using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit.T6ZM
{
    internal enum ReaderStatus : uint { NoProcess = 0, Unsupported = 1, NoLevel = 2, Resolving = 3, Ready = 4 }

    internal enum ReaderProfile : uint { Songs = 0, Rounds = 1, PackAPunch = 2, Buildables = 3, Rainbow = 4, EasterEgg = 5 }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
    internal struct ReaderSample
    {
        public ReaderStatus Status;
        public uint Clock;
        public uint Generation;
        public uint WatchKnown;
        public uint WatchSet;
        public uint Retries;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public int[] Values;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Map;

        public bool Known(int watch) => (WatchKnown & (1u << watch)) != 0;
    }

    internal static class Reader
    {
        const string Dll = "t6zm_reader.dll";
        const int ApiVersion = 5;

        static Reader()
        {
            string components = Path.GetDirectoryName(typeof(Reader).Assembly.Location);
            string path = Path.Combine(components, Environment.Is64BitProcess ? "x64" : "x86", Dll);
            if (LoadLibrary(path) == IntPtr.Zero)
                throw new DllNotFoundException($"Could not load {path} (error {Marshal.GetLastWin32Error()}).");
            if (t6r_api_version() != ApiVersion)
                throw new InvalidOperationException("t6zm_reader.dll does not match this component version.");
        }

        public static ReaderSample Poll(ReaderProfile profile)
        {
            t6r_poll((uint)profile, out ReaderSample sample);
            return sample;
        }

        public static void Close() => t6r_close();

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibrary(string path);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int t6r_api_version();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void t6r_poll(uint profile, out ReaderSample sample);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void t6r_close();
    }
}
