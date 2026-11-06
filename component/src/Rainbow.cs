using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct RainbowStep
    {
        public RainbowStep(string label, int watch, int level)
        {
            Label = label;
            Watch = watch;
            Level = level;
        }

        public string Label { get; }
        public int Watch { get; }
        public int Level { get; }
    }

    internal sealed class RainbowMap
    {
        public RainbowMap(string script, string name, params RainbowStep[] steps)
        {
            Script = script;
            Name = name;
            Steps = steps;
        }

        public string Script { get; }
        public string Name { get; }
        public IReadOnlyList<RainbowStep> Steps { get; }
        public uint Every => (uint)((1UL << Steps.Count) - 1);
    }

    internal static class RainbowMaps
    {
        public static readonly RainbowMap[] All =
        {
            new RainbowMap("zm_buried", "Buried",
                Enumerable.Range(1, 7).Select(n => new RainbowStep(n == 1 ? "1 perk" : n + " perks", RainbowLogic.PerksWatch, n)).ToArray()),
            new RainbowMap("zm_tomb", "Origins",
                new RainbowStep("Golden Shovel", RainbowLogic.ShovelWatch, 1),
                new RainbowStep("Bottle 1", RainbowLogic.SlotsWatch, 5), new RainbowStep("Bottle 2", RainbowLogic.SlotsWatch, 6),
                new RainbowStep("Bottle 3", RainbowLogic.SlotsWatch, 7), new RainbowStep("Bottle 4", RainbowLogic.SlotsWatch, 8),
                new RainbowStep("8 perks", RainbowLogic.PerksWatch, 8)),
        };

        public static RainbowMap For(string script) => All.FirstOrDefault(m => m.Script == script);
    }

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

        public override bool SupportsMap(string script) => RainbowMaps.For(script) != null;

        public override string StartBlocked => players > 1 ? "solo only" : null;

        protected override void Reset()
        {
            Array.Clear(peak, 0, peak.Length);
            done = false;
        }

        protected override void OnNewLevel() => Reset();

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            RainbowMap current = RainbowMaps.For(map);
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
                RainbowMap current = RainbowMaps.For(map);
                if (current == null) return "map not supported";
                if (players != 1) return "solo only";
                if (done) return "done";
                RainbowStep last = current.Steps[current.Steps.Count - 1];
                return $"{last.Label}: {Math.Max(0, last.Level - peak[last.Watch])} to go";
            }
        }
    }
}
