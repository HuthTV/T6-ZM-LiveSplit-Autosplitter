using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal enum ActionKind
    {
        Start,
        Split,
        Rebase,
        Pause,
        Skip,
    }

    internal readonly struct TimerAction
    {
        public TimerAction(bool start, uint tick, string label) : this(start ? ActionKind.Start : ActionKind.Split, tick, label, 0) { }

        public TimerAction(ActionKind kind, uint tick, string label, uint baseTicks)
        {
            Kind = kind;
            Tick = tick;
            Label = label;
            BaseTicks = baseTicks;
        }

        public ActionKind Kind { get; }
        public bool Start => Kind == ActionKind.Start;
        public uint Tick { get; }
        public string Label { get; }
        public uint BaseTicks { get; }
    }

    internal abstract class RunLogic
    {
        protected const int BlackscreenWatch = 0;
        protected const int CorpseWatch = 6;
        const uint AfterlifeUnfreezeTicks = 30;

        public abstract ReaderProfile Profile { get; }

        public uint? PendingStart { get; private set; }
        public uint PendingGeneration { get; private set; }

        protected bool BlackscreenEnded(GameEvent e)
        {
            if (Became(e, CorpseWatch) && PendingStart == null)
            {
                PendingStart = e.Clock + AfterlifeUnfreezeTicks;
                PendingGeneration = e.Generation;
            }
            return Became(e, BlackscreenWatch);
        }

        public void NewLevel()
        {
            PendingStart = null;
            OnNewLevel();
        }

        protected virtual void OnNewLevel() { }

        public virtual bool Continues => false;

        public virtual uint? FrozenGameTicks => null;

        public virtual bool OnSample(ReaderSample sample) => false;

        public virtual void TimerReset() { }

        public virtual bool CanResume => false;

        public virtual string Identity => GetType().Name;
        public virtual string SaveState() => "";
        public virtual int Resume(string saved, IReadOnlyList<string> splits, ReaderSample sample) => 0;

        public virtual bool SupportsMap(string script) => true;

        public virtual string StartBlocked => null;

        public TimerAction StartRun(uint tick)
        {
            PendingStart = null;
            Reset();
            return new TimerAction(true, tick, "start");
        }

        protected abstract void Reset();

        public abstract IEnumerable<TimerAction> OnChange(GameEvent e, bool running);

        public abstract string Next { get; }

        protected static bool Became(GameEvent e, int watch) => e.Watch == watch && e.OldValue == 0 && e.NewValue != 0;
    }

    internal sealed class PackAPunchLogic : RunLogic
    {
        const int FirstSlotWatch = 1;

        bool split;

        public override ReaderProfile Profile => ReaderProfile.PackAPunch;

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
            }
            else if (!split && e.Watch >= FirstSlotWatch && e.Watch < FirstSlotWatch + 4 && Became(e, e.Watch))
            {
                split = true;
                yield return new TimerAction(false, e.Clock, $"player {e.Watch - FirstSlotWatch + 1} pack-a-punch");
            }
        }

        protected override void Reset() => split = false;

        public override string Next => split ? "done" : "a player takes a PaP weapon";
    }

    internal enum RoundMode
    {
        RoundEnd = 1,
        FadeDone = 2,
        RoundStart = 3,
    }

    internal sealed class RoundLogic : RunLogic
    {
        const int RoundWatch = 1;
        const int RoundStartWatch = 2;

        int finalRound;
        RoundMode splitMode;
        RoundMode finalMode;
        bool highRound;
        readonly SortedSet<int> milestones = new SortedSet<int>();
        readonly Dictionary<int, uint> reached = new Dictionary<int, uint>();
        readonly HashSet<int> eligible = new HashSet<int>();
        readonly HashSet<int> undone = new HashSet<int>();
        int current;
        readonly List<uint> roundStarts = new List<uint>();
        readonly List<int> history = new List<int>();
        int released;
        uint lastTick;

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

        public int? ConsumeNext()
        {
            if (!(NextMilestone is int round)) return null;
            released = round;
            history.Add(round);
            return round;
        }

        public void UndoLast(int queued = 0)
        {
            int index = history.Count - 1 - queued;
            if (index < 0) return;
            int round = history[index];
            history.RemoveAt(index);
            released = history.Count > 0 ? history.Max() : 0;
            eligible.Remove(round);
            if (round <= current) undone.Add(round);
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
                for (int round = Math.Max(e.OldValue + 1, 2); round <= Math.Min(e.NewValue, 255); round++)
                {
                    reached[round] = e.Clock;
                    if (milestones.Contains(round)) eligible.Add(round);
                }
            }
            else if (e.Watch == RoundStartWatch)
                roundStarts.Add(e.Clock);

            foreach (TimerAction action in Release()) yield return action;
        }

        protected override void Reset()
        {
            reached.Clear();
            eligible.Clear();
            undone.Clear();
            current = 0;
            roundStarts.Clear();
            history.Clear();
            released = 0;
            lastTick = 0;
        }

        protected override void OnNewLevel() => Reset();

        public override bool CanResume => true;

        public override string SaveState() => string.Join(",", undone.OrderBy(r => r));

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
            released = last;
            undone.Clear();
            foreach (string part in (saved ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out int gone)) undone.Add(gone);
            int now = sample.Known(RoundWatch) ? sample.Values[RoundWatch] : 0;
            current = Math.Max(current, now);
            int skipped = 0;
            foreach (int milestone in milestones.Where(r => r > released && r <= now && !undone.Contains(r)))
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
                if (!(NextMilestone is int round)) return highRound ? "unlimited: no more splits" : "done";
                return $"round {round} ({ModeName(ModeOf(round))})";
            }
        }
    }
}
