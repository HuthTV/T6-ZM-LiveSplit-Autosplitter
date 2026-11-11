using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
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

        public override bool OnSample(ReaderSample sample)
        {
            SetMap(sample.Map);
            return false;
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

            if (e.Watch > BlackscreenWatch && e.Watch < peak.Length && e.NewValue > peak[e.Watch])
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
