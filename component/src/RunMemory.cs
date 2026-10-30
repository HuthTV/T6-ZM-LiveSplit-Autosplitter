using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class SavedSplit
    {
        public string Label;
        public long? RealTicks;
        public long? GameTicks;
    }

    internal sealed class SavedRun
    {
        public string Identity;
        public string Map = "";
        public uint ProcessId;
        public long ProcessStartTicks;
        public uint LevelId;
        public uint StartClock;
        public uint BaseTicks;
        public uint LastClock;
        public long AttemptStartedUtc;
        public int Segments;
        public bool Paused;
        public long PausedAtTicks;
        public List<SavedSplit> Splits = new List<SavedSplit>();
        public string LogicState = "";

        static string Clean(string text) => (text ?? "").Replace("\r", " ").Replace("\n", " ");

        public string Serialize()
        {
            var lines = new List<string>
            {
                "t6zm-run 1",
                "identity=" + Clean(Identity),
                "map=" + Clean(Map),
                "process=" + ProcessId,
                "processstart=" + ProcessStartTicks,
                "level=" + LevelId,
                "startclock=" + StartClock,
                "base=" + BaseTicks,
                "lastclock=" + LastClock,
                "started=" + AttemptStartedUtc,
                "segments=" + Segments,
                "paused=" + (Paused ? 1 : 0),
                "pausedat=" + PausedAtTicks,
                "logic=" + Clean(LogicState),
            };
            foreach (SavedSplit split in Splits)
                lines.Add("split=" + Clean(split.Label).Replace("|", "/") + "|" + (split.RealTicks?.ToString(CultureInfo.InvariantCulture) ?? "-") + "|" + (split.GameTicks?.ToString(CultureInfo.InvariantCulture) ?? "-"));
            return string.Join("\n", lines) + "\n";
        }

        public static SavedRun Parse(string text)
        {
            if (text == null) return null;
            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
            if (lines.Length == 0 || lines[0] != "t6zm-run 1") return null;
            var run = new SavedRun();
            try
            {
                foreach (string line in lines.Skip(1))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq), value = line.Substring(eq + 1);
                    switch (key)
                    {
                        case "identity": run.Identity = value; break;
                        case "map": run.Map = value; break;
                        case "process": run.ProcessId = uint.Parse(value, CultureInfo.InvariantCulture); break;
                        case "processstart": run.ProcessStartTicks = long.Parse(value, CultureInfo.InvariantCulture); break;
                        case "level": run.LevelId = uint.Parse(value, CultureInfo.InvariantCulture); break;
                        case "startclock": run.StartClock = uint.Parse(value, CultureInfo.InvariantCulture); break;
                        case "base": run.BaseTicks = uint.Parse(value, CultureInfo.InvariantCulture); break;
                        case "lastclock": run.LastClock = uint.Parse(value, CultureInfo.InvariantCulture); break;
                        case "started": run.AttemptStartedUtc = long.Parse(value, CultureInfo.InvariantCulture); break;
                        case "segments": run.Segments = int.Parse(value, CultureInfo.InvariantCulture); break;
                        case "paused": run.Paused = value == "1"; break;
                        case "pausedat": run.PausedAtTicks = long.Parse(value, CultureInfo.InvariantCulture); break;
                        case "logic": run.LogicState = value; break;
                        case "split":
                            string[] parts = value.Split('|');
                            if (parts.Length != 3) return null;
                            run.Splits.Add(new SavedSplit
                            {
                                Label = parts[0],
                                RealTicks = parts[1] == "-" ? (long?)null : long.Parse(parts[1], CultureInfo.InvariantCulture),
                                GameTicks = parts[2] == "-" ? (long?)null : long.Parse(parts[2], CultureInfo.InvariantCulture),
                            });
                            break;
                    }
                }
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
            return string.IsNullOrEmpty(run.Identity) || run.AttemptStartedUtc == 0 ? null : run;
        }

        public bool Matches(ReaderSample s, long processStartTicks, string identity, int segments)
        {
            return s.Status >= ReaderStatus.Ready && Identity == identity && Segments == segments && ProcessId != 0 && ProcessId == s.ProcessId
                && ProcessStartTicks == processStartTicks && LevelId == s.LevelId && Map == s.Map && s.Clock >= LastClock;
        }
    }

    internal sealed class RunStore
    {
        readonly string path;

        public RunStore(string path = null)
        {
            this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveSplit.T6ZM", "run.txt");
        }

        public SavedRun Load()
        {
            try
            {
                return File.Exists(path) ? SavedRun.Parse(File.ReadAllText(path)) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Save(SavedRun run)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, run.Serialize());
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
