using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct SongStep
    {
        public const int Event = -1;

        public SongStep(string label, int watch, int level, bool extra = false)
        {
            Label = label;
            Watch = watch;
            Level = level;
            Extra = extra;
        }

        public string Label { get; }
        public int Watch { get; }
        public int Level { get; }
        public bool Extra { get; }
    }

    internal sealed class SongMap
    {
        public SongMap(string key, string script, string name, params SongStep[] steps)
        {
            Key = key;
            Script = script;
            Name = name;
            Steps = steps;
        }

        public string Key { get; }
        public string Script { get; }
        public string Name { get; }
        public string Note => null;
        public IReadOnlyList<SongStep> Steps { get; }
        public uint Every => (uint)((1UL << Steps.Count) - 1);

        public uint Forced => 1u << (Steps.Count - 1);

        public uint Default
        {
            get
            {
                uint mask = 0;
                for (int i = 0; i < Steps.Count; i++)
                    if (!Steps[i].Extra) mask |= 1u << i;
                return mask | Forced;
            }
        }
    }

    internal static class SongMaps
    {
        static SongStep[] Counted(string what, int watch) =>
            Enumerable.Range(1, 3).Select(n => new SongStep($"{what} {n}", watch, n)).ToArray();

        static SongMap Song(string key, string script, string name, params SongStep[][] groups) =>
            new SongMap(key, script, name, groups.SelectMany(g => g).ToArray());

        static SongStep[] Start(string song) => new[] { new SongStep(song, SongStep.Event, 0) };

        static SongStep[] Rounds(int last) =>
            Enumerable.Range(1, last).Select(k => new SongStep($"End of round {k}", SongsLogic.RoundWatch, k + 1, true)).ToArray();

        static SongStep[] Extra(string label, int watch) => new[] { new SongStep(label, watch, 1, true) };

        public static readonly SongMap[] All =
        {
            Song("transit_carrion", "zm_transit", "(TranZit) Carrion", Counted("Teddy", SongsLogic.MeteorWatch)),
            Song("nuked_samantha", "zm_nuked", "(Nuketown) Samantha's Lullaby", Rounds(2), Counted("Teddy", SongsLogic.MeteorWatch)),
            Song("nuked_redamned", "zm_nuked", "(Nuketown) Re-Damned", Rounds(6), Start("Re-Damned")),
            Song("nuked_cominghome", "zm_nuked", "(Nuketown) Coming Home", Rounds(6), Start("Coming Home")),
            Song("highrise_fall", "zm_highrise", "(Die Rise) We All Fall Down", Counted("Teddy", SongsLogic.MeteorWatch)),
            Song("prison_rustycage", "zm_prison", "(Mob of the Dead) Rusty Cage", Counted("Bottle", SongsLogic.MeteorWatch)),
            Song("prison_whereare", "zm_prison", "(Mob of the Dead) Where Are We Going", Start("Afterlife revive"), Start("Where Are We Going")),
            Song("buried_running", "zm_buried", "(Buried) Always Running", Counted("Teddy", SongsLogic.MeteorWatch)),
            Song("tomb_shepherd", "zm_tomb", "(Origins) Shepherd of Fire", Rounds(4), Counted("Radio", SongsLogic.RadioWatch)),
            Song("tomb_archangel", "zm_tomb", "(Origins) Archangel", Rounds(4), Counted("Green stone", SongsLogic.MeteorWatch)),
            Song("tomb_aether", "zm_tomb", "(Origins) Aether", Extra("No Man's Land", SongsLogic.NmlWatch), Rounds(4),
                new[] { new SongStep("Aether", SongsLogic.AetherWatch, 3) }),
        };

        public static SongMap ByKey(string key) => All.FirstOrDefault(m => m.Key == key);
    }

    internal sealed class SongsLogic : RunLogic
    {
        public const int MeteorWatch = 1;
        public const int AetherWatch = 2;
        public const int RadioWatch = 3;
        const int MusicWatch = 4;
        const int NixieWatch = 5;
        const int MannequinWatch = 7;
        public const int RoundWatch = 8;
        public const int NmlWatch = 9;
        const int MeteorSongStart = 3;

        readonly SongMap song;
        readonly Func<string, uint> chosen;
        readonly int[] peak = new int[10];
        string map = "";
        uint done;
        string last;
        uint? musicRise, nixieRise;
        uint? meteorSongClock;
        bool mannequinsDone, comingHome;

        public SongsLogic(SongMap song, Func<string, uint> chosen)
        {
            this.song = song;
            this.chosen = chosen;
        }

        public override ReaderProfile Profile => ReaderProfile.Songs;

        public void SetMap(string script)
        {
            if (!string.IsNullOrEmpty(script)) map = script;
        }

        public override bool SupportsMap(string script) => script == song.Script;

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
            SongMap current = song;
            if (!SupportsMap(map)) yield break;
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }

            uint pick = chosen(song.Key);
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
                if (e.Watch == CorpseWatch && e.OldValue != 0 && e.NewValue == 0) return "Afterlife revive";
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
                SongMap current = song;
                uint pick = chosen(song.Key) & current.Every;
                int total = Enumerable.Range(0, current.Steps.Count).Count(i => (pick & (1u << i)) != 0);
                int split = Enumerable.Range(0, current.Steps.Count).Count(i => (pick & done & (1u << i)) != 0);
                return last == null ? $"{total} song steps" : $"{split} of {total} song steps, last: {last}";
            }
        }
    }
}
