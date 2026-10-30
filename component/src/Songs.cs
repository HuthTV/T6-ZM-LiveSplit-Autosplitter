using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct SongStep
    {
        public const int Event = -1;

        public SongStep(string label, int watch, int level)
        {
            Label = label;
            Watch = watch;
            Level = level;
        }

        public string Label { get; }
        public int Watch { get; }
        public int Level { get; }
    }

    internal sealed class SongMap
    {
        public SongMap(string script, string name, string note, params SongStep[] steps)
        {
            Script = script;
            Name = name;
            Note = note;
            Steps = steps;
        }

        public string Script { get; }
        public string Name { get; }
        public string Note { get; }
        public IReadOnlyList<SongStep> Steps { get; }
        public uint Every => (uint)((1UL << Steps.Count) - 1);
    }

    internal static class SongMaps
    {
        static SongStep[] Counted(string song, string what, int watch) =>
            Enumerable.Range(1, 3).Select(n => new SongStep($"{song}: {what} {n}", watch, n)).ToArray();

        static SongMap Map(string script, string name, params SongStep[][] groups) =>
            new SongMap(script, name, null, groups.SelectMany(g => g).ToArray());

        static SongStep[] Start(string song) => new[] { new SongStep(song, SongStep.Event, 0) };

        public static readonly SongMap[] All =
        {
            Map("zm_transit", "TranZit", Counted("Carrion", "teddy", SongsLogic.MeteorWatch)),
            Map("zm_nuked", "Nuketown", Counted("Samantha's Lullaby", "teddy", SongsLogic.MeteorWatch), Start("Coming Home"), Start("Re-Damned")),
            Map("zm_highrise", "Die Rise", Counted("We All Fall Down", "teddy", SongsLogic.MeteorWatch)),
            Map("zm_prison", "Mob of the Dead", Counted("Rusty Cage", "bottle", SongsLogic.MeteorWatch), Start("Where Are We Going")),
            Map("zm_buried", "Buried", Counted("Always Running", "teddy", SongsLogic.MeteorWatch)),
            Map("zm_tomb", "Origins", Counted("Archangel", "green stone", SongsLogic.MeteorWatch), Counted("Aether", "plate", SongsLogic.AetherWatch),
                Counted("Shepherd of Fire", "radio", SongsLogic.RadioWatch)),
        };

        public static SongMap For(string script) => All.FirstOrDefault(m => m.Script == script);
    }

    internal sealed class SongsLogic : RunLogic
    {
        public const int MeteorWatch = 1;
        public const int AetherWatch = 2;
        public const int RadioWatch = 3;
        const int MusicWatch = 4;
        const int NixieWatch = 5;
        const int MannequinWatch = 7;
        const int MeteorSongStart = 3;

        readonly Func<string, uint> chosen;
        readonly int[] peak = new int[8];
        string map = "";
        uint done;
        string last;
        uint? musicRise, nixieRise;
        uint? meteorSongClock;
        bool mannequinsDone, comingHome;

        public SongsLogic(Func<string, uint> chosen) => this.chosen = chosen;

        public override ReaderProfile Profile => ReaderProfile.Songs;

        public void SetMap(string script)
        {
            if (!string.IsNullOrEmpty(script)) map = script;
        }

        public override bool SupportsMap(string script) => SongMaps.For(script) != null;

        protected override void Reset()
        {
            Array.Clear(peak, 0, peak.Length);
            done = 0;
            last = null;
            musicRise = nixieRise = meteorSongClock = null;
            mannequinsDone = comingHome = false;
        }

        protected override void OnNewLevel() => Reset();

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            SongMap current = SongMaps.For(map);
            if (current == null) yield break;
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }

            uint pick = chosen(map);
            if (e.Watch == MeteorWatch && e.OldValue < MeteorSongStart && e.NewValue >= MeteorSongStart) meteorSongClock = e.Clock;

            if (e.Watch > 0 && e.Watch < peak.Length && e.NewValue > peak[e.Watch])
            {
                int before = peak[e.Watch];
                peak[e.Watch] = e.NewValue;
                for (int i = 0; i < current.Steps.Count; i++)
                {
                    SongStep step = current.Steps[i];
                    if (step.Watch != e.Watch || step.Level <= before || step.Level > e.NewValue) continue;
                    if (Take(i, pick, current)) yield return new TimerAction(false, e.Clock, step.Label);
                }
            }

            string title = SongEvent(e);
            if (title == null) yield break;
            for (int i = 0; i < current.Steps.Count; i++)
                if (current.Steps[i].Watch == SongStep.Event && current.Steps[i].Label == title && Take(i, pick, current))
                    yield return new TimerAction(false, e.Clock, title);
        }

        bool Take(int index, uint pick, SongMap current)
        {
            uint bit = 1u << index;
            if ((done & bit) != 0) return false;
            done |= bit;
            if ((pick & bit) == 0) return false;
            last = current.Steps[index].Label;
            return true;
        }

        string SongEvent(GameEvent e)
        {
            if (map == "zm_nuked")
            {
                if (e.Watch == MannequinWatch && e.OldValue > 0 && e.NewValue == 0) mannequinsDone = true;
                if (!Became(e, MusicWatch) || meteorSongClock == e.Clock) return null;
                if (mannequinsDone && !comingHome)
                {
                    comingHome = true;
                    return "Coming Home";
                }
                return "Re-Damned";
            }
            if (map == "zm_prison")
            {
                if (Became(e, MusicWatch)) musicRise = e.Clock;
                else if (Became(e, NixieWatch)) nixieRise = e.Clock;
                else return null;
                if (musicRise.HasValue && musicRise == nixieRise)
                {
                    musicRise = nixieRise = null;
                    return "Where Are We Going";
                }
            }
            return null;
        }

        public override string Next
        {
            get
            {
                SongMap current = SongMaps.For(map);
                if (current == null) return "map not supported";
                uint pick = chosen(map) & current.Every;
                int total = Enumerable.Range(0, current.Steps.Count).Count(i => (pick & (1u << i)) != 0);
                int split = Enumerable.Range(0, current.Steps.Count).Count(i => (pick & done & (1u << i)) != 0);
                return last == null ? $"{total} song steps" : $"{split} of {total} song steps, last: {last}";
            }
        }
    }
}
