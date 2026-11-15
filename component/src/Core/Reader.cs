using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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
        public uint ProcessId;
        public uint LevelId;
        public uint ClockDips;
        public uint VelocityKnown;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Velocity;
        public float FrameMs;

        public bool Known(int watch) => (WatchKnown & (1u << watch)) != 0;
    }

    internal static class Reader
    {
        const string Dll = "t6zm_reader.dll";
        const int ApiVersion = 8;

        static Reader()
        {
            string resource = Environment.Is64BitProcess ? "t6zm_reader.x64.dll" : "t6zm_reader.x86.dll";
            byte[] image;
            using (Stream stream = typeof(Reader).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) throw new DllNotFoundException($"{resource} is missing from the component.");
                image = new byte[stream.Length];
                int read = 0;
                while (read < image.Length)
                {
                    int got = stream.Read(image, read, image.Length - read);
                    if (got <= 0) throw new IOException($"Could not read {resource}.");
                    read += got;
                }
            }
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(image), 0, 8).Replace("-", "");
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiveSplit.T6ZM", hash);
            string path = Path.Combine(folder, Dll);
            if (!File.Exists(path) || new FileInfo(path).Length != image.Length)
            {
                Directory.CreateDirectory(folder);
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temp, image);
                try { File.Move(temp, path); }
                catch (IOException) { File.Delete(temp); }
            }
            if (LoadLibrary(path) == IntPtr.Zero)
                throw new DllNotFoundException($"Could not load {path} (error {Marshal.GetLastWin32Error()}).");
            if (t6r_api_version() != ApiVersion)
                throw new InvalidOperationException("t6zm_reader.dll does not match this component version.");
            RemoveOldVersions(Path.GetDirectoryName(folder), hash);
        }

        static void RemoveOldVersions(string root, string current)
        {
            try
            {
                foreach (string folder in Directory.GetDirectories(root))
                    if (!string.Equals(Path.GetFileName(folder), current, StringComparison.OrdinalIgnoreCase))
                        try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
