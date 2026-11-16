using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal enum RoundMode
    {
        RoundEnd = 1,
        FadeDone = 2,
        RoundStart = 3,
    }

    internal sealed class RoundSettings : LiveSettings
    {
        public RoundSettings(IEnumerable<int> splitRounds, int maxRound, bool unlimited, RoundMode splitTiming, RoundMode lastTiming)
        {
            SplitRounds = splitRounds;
            MaxRound = maxRound;
            Unlimited = unlimited;
            SplitTiming = splitTiming;
            LastTiming = lastTiming;
        }

        public IEnumerable<int> SplitRounds { get; }
        public int MaxRound { get; }
        public bool Unlimited { get; }
        public RoundMode SplitTiming { get; }
        public RoundMode LastTiming { get; }
    }

    internal sealed class RoundLogic : RunLogic
    {
        internal const int RoundWatch = 1;
        const int RoundStartWatch = 2;

        int finalRound;
        RoundMode splitMode;
        RoundMode finalMode;
        bool highRound;
        readonly SortedSet<int> milestones = new SortedSet<int>();
        readonly Dictionary<int, uint> reached = new Dictionary<int, uint>();
        readonly HashSet<int> eligible = new HashSet<int>();
        int current;
        readonly List<uint> roundStarts = new List<uint>();
        readonly List<int> history = new List<int>();
        int released;
        uint lastTick;
        bool betweenRounds;

        public RoundLogic(IEnumerable<int> splitRounds, int finalRound, bool highRound, RoundMode splitMode, RoundMode finalMode)
        {
            this.finalRound = finalRound;
            this.highRound = highRound;
            this.splitMode = splitMode;
            this.finalMode = finalMode;
            SetSplitRounds(splitRounds);
        }

        public override ReaderProfile Profile => ReaderProfile.Rounds;

        public int SplitCount => milestones.Count;

        public void SetLimits(int finalRound, bool highRound)
        {
            this.finalRound = finalRound;
            this.highRound = highRound;
        }

        public bool Keeps(int round) => milestones.Contains(round);

        public void Withdraw(int round)
        {
            history.Remove(round);
            eligible.Remove(round);
            released = history.Count > 0 ? history.Max() : 0;
        }

        public string Describe(string label)
        {
            if (!(RoundOf(label) is int round)) return label;
            return $"round {round} ({ModeName(ModeOf(round))})";
        }

        public static int? RoundOf(string label) =>
            label != null && label.StartsWith("round ") && int.TryParse(label.Substring(6), out int round) ? round : (int?)null;

        static string ModeName(RoundMode mode) => mode == RoundMode.RoundEnd ? "round end" : mode == RoundMode.FadeDone ? "text fade" : "round start";

        public bool BetweenRounds => betweenRounds;

        public override bool HoldsLiveSettings => BetweenRounds;

        public override void ApplyLiveSettings(LiveSettings values, Queue<TimerAction> waiting, bool running)
        {
            if (!(values is RoundSettings settings)) return;
            SetLimits(settings.MaxRound, settings.Unlimited);
            SetSplitRounds(settings.SplitRounds);
            SetModes(settings.SplitTiming, settings.LastTiming);
            var kept = new Queue<TimerAction>();
            foreach (TimerAction queued in waiting)
            {
                int queuedRound = RoundOf(queued.Label) ?? 0;
                if (queued.Kind == ActionKind.Split && queuedRound > 0 && !Keeps(queuedRound)) Withdraw(queuedRound);
                else kept.Enqueue(queued);
            }
            waiting.Clear();
            foreach (TimerAction queued in kept) waiting.Enqueue(queued);
            if (running) foreach (TimerAction action in Release()) waiting.Enqueue(action);
        }

        public override string StatusDetail(ReaderSample sample) => sample.Known(RoundWatch) ? $"round {sample.Values[RoundWatch]}" : null;

        public override string NextWaiting(TimerAction waiting) => Describe(waiting.Label);

        public void SetModes(RoundMode split, RoundMode final)
        {
            splitMode = split;
            finalMode = final;
        }

        public void SetSplitRounds(IEnumerable<int> splitRounds)
        {
            milestones.Clear();
            milestones.UnionWith(splitRounds.Where(r => r >= 2 && r <= 255 && (highRound || r <= finalRound)));
            if (!highRound && milestones.Count == 0) milestones.Add(finalRound);
        }

        RoundMode ModeOf(int round) => !highRound && round == milestones.Max ? finalMode : splitMode;

        public static uint FadeTicks(int round)
        {
            int m = Math.Min(round, 100);
            int pulses = 2 + (100 - m + 19) / 20;
            return (uint)((2000 + 1000 * pulses) / 50);
        }

        uint? SplitTick(int round)
        {
            if (!reached.TryGetValue(round, out uint tick)) return null;
            switch (ModeOf(round))
            {
                case RoundMode.RoundEnd: return tick;
                case RoundMode.FadeDone: return tick + FadeTicks(round);
                default:
                    foreach (uint start in roundStarts)
                        if (start >= tick) return start;
                    return null;
            }
        }

        bool Open(int round) => round > current || eligible.Contains(round);

        int? NextMilestone => milestones.Where(r => r > released && Open(r)).Cast<int?>().FirstOrDefault();

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }

            if (e.Watch == RoundWatch && e.NewValue > e.OldValue)
            {
                current = Math.Max(current, e.NewValue);
                betweenRounds = true;
                for (int round = Math.Max(e.OldValue + 1, 2); round <= Math.Min(e.NewValue, 255); round++)
                {
                    reached[round] = e.Clock;
                    if (milestones.Contains(round)) eligible.Add(round);
                }
            }
            else if (e.Watch == RoundStartWatch)
            {
                roundStarts.Add(e.Clock);
                betweenRounds = false;
            }

            foreach (TimerAction action in Release()) yield return action;
        }

        protected override void Reset()
        {
            reached.Clear();
            eligible.Clear();
            current = 0;
            roundStarts.Clear();
            history.Clear();
            released = 0;
            lastTick = 0;
            betweenRounds = false;
        }

        protected override void OnNewLevel() => Reset();

        public override bool CanResume => true;

        const string ReleasedTag = "r";

        public override string SaveState() => ReleasedTag + released;

        public override int Resume(string saved, IReadOnlyList<string> splits, ReaderSample sample)
        {
            int last = 0;
            history.Clear();
            foreach (string label in splits)
                if (RoundOf(label) is int round)
                {
                    last = Math.Max(last, round);
                    history.Add(round);
                }
            released = saved != null && saved.StartsWith(ReleasedTag) && int.TryParse(saved.Substring(ReleasedTag.Length), out int kept) && kept >= 0
                ? Math.Max(kept, last) : last;
            int now = sample.Known(RoundWatch) ? sample.Values[RoundWatch] : 0;
            current = Math.Max(current, now);
            int skipped = 0;
            foreach (int milestone in milestones.Where(r => r > released && r <= now))
            {
                skipped++;
                released = milestone;
                history.Add(milestone);
            }
            return skipped;
        }

        public IEnumerable<TimerAction> Release()
        {
            while (NextMilestone is int round && SplitTick(round) is uint tick)
            {
                tick = Math.Max(tick, lastTick);
                lastTick = tick;
                released = round;
                history.Add(round);
                yield return new TimerAction(false, tick, "round " + round);
            }
        }

        public override string Next
        {
            get
            {
                if (!(NextMilestone is int round)) return highRound ? "no more splits" : "done";
                return $"round {round} ({ModeName(ModeOf(round))})";
            }
        }
    }
}
