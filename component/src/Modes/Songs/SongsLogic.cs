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
        uint? musicRise, nixieRise;
        uint? meteorSongClock;
        bool mannequinsDone, comingHome;
        readonly HashSet<string> songsStarted = new HashSet<string>();

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

        bool Finished => (done & song.Forced) != 0;

        protected override void Reset()
        {
            Array.Clear(peak, 0, peak.Length);
            done = 0;
            musicRise = nixieRise = meteorSongClock = null;
            mannequinsDone = comingHome = false;
            songsStarted.Clear();
        }

        protected override void OnNewLevel() => Reset();

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!SupportsMap(map)) yield break;
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }

            if (Finished) yield break;
            uint pick = chosen(song.Key);
            if (e.Watch == MeteorWatch && e.OldValue < MeteorSongStart && e.NewValue >= MeteorSongStart) meteorSongClock = e.Clock;
            if (song.Parts != null)
            {
                if (e.Watch > BlackscreenWatch && e.Watch < peak.Length) peak[e.Watch] = Math.Max(peak[e.Watch], e.NewValue);
                if (SongEvent(e) is string started) songsStarted.Add(started);
                if (PartsDone() < song.Parts.Length) yield break;
                done |= song.Forced;
                yield return new TimerAction(false, e.Clock, SongMaps.AllSongs);
                yield break;
            }

            if (e.Watch > BlackscreenWatch && e.Watch < peak.Length && e.NewValue > peak[e.Watch])
            {
                int before = peak[e.Watch];
                peak[e.Watch] = e.NewValue;
                for (int i = 0; i < song.Steps.Count; i++)
                {
                    SongStep step = song.Steps[i];
                    if (step.Watch != e.Watch || step.Level <= before || step.Level > e.NewValue) continue;
                    if (Take(i, pick)) yield return new TimerAction(false, e.Clock, step.Label);
                }
            }

            string title = SongEvent(e);
            if (title == null || Finished) yield break;
            for (int i = 0; i < song.Steps.Count; i++)
                if (song.Steps[i].Watch == SongStep.Event && song.Steps[i].Label == title && Take(i, pick))
                    yield return new TimerAction(false, e.Clock, title);
        }

        int PartsDone()
        {
            int count = 0;
            foreach (string key in song.Parts)
            {
                SongStep last = SongMaps.ByKey(key).Steps.Last();
                if (last.Watch == SongStep.Event ? songsStarted.Contains(last.Label) : peak[last.Watch] >= last.Level) count++;
            }
            return count;
        }

        bool Take(int index, uint pick)
        {
            uint bit = 1u << index;
            if ((done & bit) != 0) return false;
            done |= bit;
            return (pick & bit) != 0;
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
                    if (Finished) return "end reached";
                if (song.Parts != null) return $"{PartsDone()} of {song.Parts.Length} songs";
                uint pick = chosen(song.Key) & song.Every;
                for (int i = 0; i < song.Steps.Count; i++)
                {
                    if ((pick & (1u << i)) == 0 || (done & (1u << i)) != 0) continue;
                    SongStep step = song.Steps[i];
                    if (step.Items != null) return $"{Math.Min(peak[step.Watch], 3)} of 3 {step.Items}";
                    if (step.Watch == RoundWatch) return "end of round " + (step.Level - 1);
                    return step.Label;
                }
                return "done";
            }
        }
    }
}
