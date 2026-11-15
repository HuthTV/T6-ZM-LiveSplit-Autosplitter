using System;
using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class RainbowLogic : RunLogic
    {
        public const int PerksWatch = 1;
        public const int SlotsWatch = 2;
        public const int ShovelWatch = 3;
        public const int PlayersWatch = 5;

        readonly Func<string, uint> chosen;
        readonly int[] peak = new int[4];
        string map = "";
        int players;
        bool done;

        public RainbowLogic(Func<string, uint> chosen) => this.chosen = chosen;

        public override ReaderProfile Profile => ReaderProfile.Rainbow;

        public void SetLevel(string script, int? playerCount)
        {
            if (!string.IsNullOrEmpty(script)) map = script;
            if (playerCount is int count) players = count;
        }

        public override bool OnSample(ReaderSample sample)
        {
            SetLevel(sample.Map, sample.Known(PlayersWatch) ? sample.Values[PlayersWatch] : (int?)null);
            return false;
        }

        public override bool SupportsMap(string script) => RainbowMaps.For(script) != null;

        public override string StartBlocked => players > 1 ? "waiting for solo game" : null;

        protected override void Reset()
        {
            Array.Clear(peak, 0, peak.Length);
            done = false;
        }

        protected override void OnNewLevel() => Reset();

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            StepList<RainbowStep> current = RainbowMaps.For(map);
            if (current == null || players != 1) yield break;
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }
            if (done || (e.Watch != PerksWatch && e.Watch != SlotsWatch && e.Watch != ShovelWatch) || e.NewValue <= peak[e.Watch]) yield break;

            int before = peak[e.Watch];
            peak[e.Watch] = e.NewValue;
            uint pick = chosen(map);
            int last = current.Steps.Count - 1;
            for (int i = 0; i < current.Steps.Count; i++)
            {
                RainbowStep step = current.Steps[i];
                if (step.Watch != e.Watch || step.Level <= before || step.Level > e.NewValue) continue;
                if ((pick & (1u << i)) == 0 && i != last) continue;
                if (i == last) done = true;
                yield return new TimerAction(false, e.Clock, step.Label);
            }
        }

        public override string Next
        {
            get
            {
                StepList<RainbowStep> current = RainbowMaps.For(map);
                if (current == null) return string.IsNullOrEmpty(map) ? "waiting for map" : $"{GameMaps.Name(map)} unsupported";
                if (players != 1) return "waiting for solo game";
                if (done) return "done";
                RainbowStep last = current.Steps[current.Steps.Count - 1];
                uint pick = chosen(map);
                for (int i = 0; i < current.Steps.Count; i++)
                {
                    RainbowStep step = current.Steps[i];
                    if (((pick & (1u << i)) == 0 && step != last) || peak[step.Watch] >= step.Level) continue;
                    if (step.Watch == PerksWatch) return $"{Math.Min(peak[PerksWatch], last.Level)} of {last.Level} perks";
                    return step.Label;
                }
                return $"{Math.Min(peak[PerksWatch], last.Level)} of {last.Level} perks";
            }
        }
    }
}
