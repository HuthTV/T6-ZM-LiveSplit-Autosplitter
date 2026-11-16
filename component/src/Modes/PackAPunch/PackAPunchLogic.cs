using System;
using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class PackAPunchLogic : RunLogic
    {
        public const string PapLabel = "PaP";
        public const int GrabWatch = 1;
        const int Slots = 4;
        public const int RoundWatch = 7;
        public const int PowerWatch = 8;
        const int ClassicWatch = 9;
        const int TownWatch = 10;
        public const int GensWatch = 11;
        public const int GhostsWatch = GensWatch;
        public const int DryerWatch = 12;
        public const int PartsWatch = 13;
        public const int PlaneWatch = 14;
        const int FirstStepWatch = 7;
        const int LastStepWatch = 14;

        readonly Func<string, uint> chosen;
        readonly int[] peak = new int[LastStepWatch + 1];
        readonly HashSet<int> passed = new HashSet<int>();
        string map = "";
        bool? classic, town;
        bool done;

        public PackAPunchLogic(Func<string, uint> chosen) => this.chosen = chosen;

        public override ReaderProfile Profile => ReaderProfile.PackAPunch;

        PapMap Current => PapMaps.For(map, classic, town);

        public void SetLevel(string script, bool? isClassic, bool? isTown)
        {
            if (!string.IsNullOrEmpty(script)) map = script;
            if (isClassic.HasValue) classic = isClassic;
            if (isTown.HasValue) town = isTown;
        }

        public override bool OnSample(ReaderSample sample)
        {
            SetLevel(sample.Map,
                sample.Known(ClassicWatch) ? sample.Values[ClassicWatch] != 0 : (bool?)null,
                sample.Known(TownWatch) ? sample.Values[TownWatch] != 0 : (bool?)null);
            return false;
        }

        public override bool SupportsMap(string script) => PapMaps.Supports(script);

        public override string StartBlocked =>
            map == "zm_transit" && classic == false && town == false ? "no Pack-a-Punch here" : null;

        protected override void Reset()
        {
            Array.Clear(peak, 0, peak.Length);
            passed.Clear();
            done = false;
        }

        protected override void OnNewLevel()
        {
            Reset();
            classic = town = null;
        }

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }
            PapMap current = Current;
            if (done || current == null) yield break;

            int last = current.Steps.Count - 1;
            var reached = new List<int>();
            if (e.Watch >= GrabWatch && e.Watch < GrabWatch + Slots)
            {
                if (Became(e, e.Watch)) reached.Add(last);
            }
            else if (e.Watch >= FirstStepWatch && e.Watch <= LastStepWatch && e.NewValue > peak[e.Watch])
            {
                int before = peak[e.Watch];
                peak[e.Watch] = e.NewValue;
                for (int i = 0; i < last; i++)
                {
                    PapStep step = current.Steps[i];
                    if (step.Watch == e.Watch && step.Level > before && step.Level <= e.NewValue) reached.Add(i);
                }
            }

            uint pick = chosen(current.Key) | current.Forced;
            foreach (int i in reached)
            {
                if (passed.Contains(i)) continue;
                passed.Add(i);
                if ((pick & (1u << i)) == 0) continue;
                for (int j = 0; j < i; j++)
                {
                    if (passed.Contains(j) || (pick & (1u << j)) == 0) continue;
                    passed.Add(j);
                    yield return new TimerAction(ActionKind.Skip, e.Clock, current.Steps[j].Label, 0);
                }
                if (i == last) done = true;
                yield return new TimerAction(false, e.Clock, current.Steps[i].Label);
            }
        }

        public override string Next
        {
            get
            {
                PapMap current = Current;
                if (current == null)
                {
                    if (string.IsNullOrEmpty(map)) return "waiting for map";
                    return map == "zm_transit" ? "no Pack-a-Punch here" : $"{GameMaps.Name(map)} unsupported";
                }
                if (done) return "done";
                uint pick = chosen(current.Key) | current.Forced;
                for (int i = 0; i < current.Steps.Count; i++)
                    if ((pick & (1u << i)) != 0 && !passed.Contains(i))
                        return i == current.Steps.Count - 1 ? "waiting for PaP grab" : current.Steps[i].Label;
                return "waiting for PaP grab";
            }
        }
    }
}
