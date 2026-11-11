using System.Collections.Generic;

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

    internal abstract class LiveSettings { }

    internal abstract class RunLogic
    {
        internal const int BlackscreenWatch = 0;
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

        public virtual void ApplyLiveSettings(LiveSettings values, Queue<TimerAction> waiting, bool running) { }

        public virtual bool HoldsLiveSettings => false;

        public virtual string ManualSegment(Queue<TimerAction> waiting) => null;

        public virtual bool ManualUndo(IReadOnlyCollection<TimerAction> waiting) => false;

        public virtual string StatusDetail(ReaderSample sample) => null;

        public virtual string NextWaiting(TimerAction waiting) => Next;

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
}
