using System;
using System.Collections.Generic;

namespace LiveSplit.T6ZM
{
    internal sealed class BuildablesLogic : RunLogic
    {
        const int MaskWatch = 1;

        readonly Func<string, uint> chosen;
        string map = "";
        uint built;
        uint settled;

        public BuildablesLogic(Func<string, uint> chosen) => this.chosen = chosen;

        public override ReaderProfile Profile => ReaderProfile.Buildables;

        public void SetMap(string script)
        {
            if (script != map && !string.IsNullOrEmpty(script)) built = settled = 0;
            if (!string.IsNullOrEmpty(script)) map = script;
        }

        public override bool OnSample(ReaderSample sample)
        {
            SetMap(sample.Map);
            return false;
        }

        public override bool SupportsMap(string script) => BuildableMaps.For(script) != null;

        protected override void Reset() => settled = built;

        protected override void OnNewLevel() => built = settled = 0;

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            BuildableMap current = BuildableMaps.For(map);
            uint before = built;
            if (e.Watch == MaskWatch) built |= (uint)(e.OldValue | e.NewValue);
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }
            if (e.Watch != MaskWatch || current == null) yield break;

            uint required = current.Required;
            uint fresh = (uint)e.NewValue & ~(uint)e.OldValue & ~settled & current.Every;
            if (fresh == 0) yield break;
            uint pick = chosen(map);
            int finalBit = (before & required) != required && (built & required) == required ? 31 - LeadingZeros(fresh & required) : -1;
            for (int bit = 0; bit < current.Steps.Count; bit++)
            {
                uint mask = 1u << bit;
                if ((fresh & mask) == 0) continue;
                settled |= mask;
                if ((pick & mask) != 0 || bit == finalBit)
                    yield return new TimerAction(false, e.Clock, current.Steps[bit].Label);
            }
        }

        public override string Next
        {
            get
            {
                BuildableMap current = BuildableMaps.For(map);
                if (current == null) return "map not supported";
                int total = Count(current.Required);
                int left = total - Count(built & current.Required);
                return left == 0 ? "done" : $"{left} of {total} buildables to go";
            }
        }

        static int LeadingZeros(uint mask)
        {
            int n = 0;
            for (uint bit = 0x80000000; (mask & bit) == 0 && n < 32; bit >>= 1) n++;
            return n;
        }

        static int Count(uint mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }
    }
}
