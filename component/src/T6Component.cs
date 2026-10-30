using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.T6ZM
{
    internal sealed class T6Component : IComponent
    {
        static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

        readonly LiveSplitState state;
        readonly TimerModel timer;
        readonly Settings settings = new Settings();
        readonly Watcher watcher;
        readonly Queue<TimerAction> pending = new Queue<TimerAction>();
        readonly InfoTextComponent statusRow = new InfoTextComponent("T6ZM", "");
        readonly InfoTextComponent nextRow = new InfoTextComponent("Next", "");

        RunLogic logic;
        bool timingMethodSet;
        int logicVersion = -1;
        int roundsVersion = -1;
        uint? startClock;
        uint startGeneration;
        uint baseTicks;
        string lastSplit;

        readonly RunStore store = new RunStore();
        readonly List<SavedSplit> madeSplits = new List<SavedSplit>();
        SavedRun saved;
        DateTime attemptStartUtc;
        string attemptMap = "";
        uint attemptProcess, attemptLevel, lastClockSeen;
        long attemptProcessStart;
        DateTime lastSave = DateTime.MinValue;
        DateTime? inactiveSince;
        DateTime closingUntil = DateTime.MinValue;

        public T6Component(LiveSplitState state)
        {
            this.state = state;
            timer = new TimerModel { CurrentState = state };
            logic = settings.CreateLogic();
            watcher = new Watcher(logic.Profile);
            state.OnReset += OnReset;
            saved = store.Load();
            if (state.Form != null) state.Form.FormClosing += (s, e) => closingUntil = DateTime.UtcNow.AddSeconds(60);
        }

        IEnumerable<InfoTextComponent> Rows => settings.ShowInfo ? new[] { statusRow, nextRow } : Array.Empty<InfoTextComponent>();

        public string ComponentName => "T6 Zombies Autosplitter";
        public float HorizontalWidth => Rows.Sum(r => r.HorizontalWidth);
        public float MinimumHeight => Rows.Select(r => r.MinimumHeight).DefaultIfEmpty(0).Max();
        public float VerticalHeight => Rows.Sum(r => r.VerticalHeight);
        public float MinimumWidth => Rows.Select(r => r.MinimumWidth).DefaultIfEmpty(0).Max();
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        bool Running => startClock.HasValue && state.CurrentPhase == TimerPhase.Running;

        bool Active => startClock.HasValue && (state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused);

        void OnReset(object sender, TimerPhase phase)
        {
            startClock = null;
            baseTicks = 0;
            lastSplit = null;
            pending.Clear();
            madeSplits.Clear();
            logic.TimerReset();
            if (inactiveSince == null) inactiveSince = DateTime.UtcNow;
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            if (!timingMethodSet && state.CurrentPhase == TimerPhase.NotRunning)
            {
                timingMethodSet = true;
                state.CurrentTimingMethod = TimingMethod.GameTime;
            }

            settings.Locked = state.CurrentPhase != TimerPhase.NotRunning;
            if (state.CurrentPhase == TimerPhase.NotRunning && settings.Version != logicVersion)
            {
                logicVersion = settings.Version;
                roundsVersion = settings.RoundsVersion;
                logic = settings.CreateLogic();
                watcher.Profile = logic.Profile;
            }
            else if (settings.RoundsVersion != roundsVersion && logic is RoundLogic rounds)
            {
                roundsVersion = settings.RoundsVersion;
                rounds.SetSplitRounds(settings.SplitRounds);
                if (Running) foreach (TimerAction action in rounds.Release()) pending.Enqueue(action);
            }

            ReaderSample sample = watcher.Latest;
            (logic as BuildablesLogic)?.SetMap(sample.Map);
            (logic as SongsLogic)?.SetMap(sample.Map);
            (logic as RainbowLogic)?.SetLevel(sample.Map, sample.Known(RainbowLogic.PlayersWatch) ? sample.Values[RainbowLogic.PlayersWatch] : 0);
            settings.GameMap = sample.Status >= ReaderStatus.Resolving ? sample.Map : null;
            if (saved != null && state.CurrentPhase == TimerPhase.NotRunning && !startClock.HasValue) TryResume(sample);
            Housekeeping();
            if (logic.OnSample(sample) && state.CurrentPhase != TimerPhase.NotRunning) timer.Reset();
            while (watcher.Events.TryDequeue(out GameEvent e))
            {
                if (e.Kind == GameEventKind.NewLevel)
                {
                    if (!logic.Continues && state.CurrentPhase != TimerPhase.NotRunning) timer.Reset();
                    pending.Clear();
                    logic.NewLevel();
                    continue;
                }
                if (Active && e.Generation != startGeneration && !logic.Continues) continue;
                foreach (TimerAction action in logic.OnChange(e, Active))
                {
                    switch (action.Kind)
                    {
                        case ActionKind.Start: Start(action, e.Generation); break;
                        case ActionKind.Rebase: Rebase(action, e.Generation); break;
                        default: pending.Enqueue(action); break;
                    }
                }
            }

            if (state.CurrentPhase == TimerPhase.NotRunning && logic.PendingStart is uint due
                && sample.Generation == logic.PendingGeneration && sample.Status >= ReaderStatus.Resolving && sample.Clock >= due)
                Start(logic.StartRun(due), sample.Generation);

            bool live = Running && sample.Generation == startGeneration && sample.Status >= ReaderStatus.NoLevel;
            while (live && Running && pending.Count > 0 && pending.Peek().Tick <= sample.Clock)
            {
                TimerAction next = pending.Dequeue();
                if (next.Kind == ActionKind.Pause)
                {
                    timer.Pause();
                    SaveRun(sample);
                    break;
                }
                if (next.Kind == ActionKind.Skip)
                {
                    madeSplits.Add(new SavedSplit { Label = "(skipped)" });
                    timer.SkipSplit();
                    SaveRun(sample);
                    continue;
                }
                state.SetGameTime(Ticks(baseTicks + next.Tick - startClock.Value));
                int index = state.CurrentSplitIndex;
                timer.Split();
                lastSplit = next.Label;
                Time made = index >= 0 && index < state.Run.Count ? state.Run[index].SplitTime : default(Time);
                madeSplits.Add(new SavedSplit { Label = next.Label, RealTicks = made.RealTime?.Ticks, GameTicks = made.GameTime?.Ticks });
                SaveRun(sample);
            }
            if (live && Running)
            {
                state.IsGameTimePaused = true;
                state.SetGameTime(Ticks(baseTicks + sample.Clock - startClock.Value));
            }
            if (Active && sample.Generation == startGeneration && sample.Status >= ReaderStatus.NoLevel) lastClockSeen = sample.Clock;
            if (Active && DateTime.UtcNow - lastSave > TimeSpan.FromSeconds(2)) SaveRun(sample);

            statusRow.InformationValue = Status(sample);
            nextRow.InformationValue = Next();
            foreach (InfoTextComponent row in Rows) row.Update(invalidator, state, width, height, mode);
        }

        void Start(TimerAction action, uint generation)
        {
            if (state.CurrentPhase != TimerPhase.NotRunning)
            {
                if (!logic.Continues) return;
                timer.Reset();
            }
            timer.Start();
            timer.InitializeGameTime();
            state.IsGameTimePaused = true;
            state.SetGameTime(Ticks(action.BaseTicks));
            startClock = action.Tick;
            startGeneration = generation;
            baseTicks = action.BaseTicks;
            pending.Clear();
            madeSplits.Clear();
            attemptStartUtc = DateTime.UtcNow;
            inactiveSince = null;
            CaptureAttempt(watcher.Latest);
            SaveRun(watcher.Latest);
        }

        void Rebase(TimerAction action, uint generation)
        {
            if (!Active) return;
            if (state.CurrentPhase == TimerPhase.Paused) timer.Pause();
            startClock = action.Tick;
            startGeneration = generation;
            baseTicks = action.BaseTicks;
            state.SetGameTime(Ticks(baseTicks));
            pending.Clear();
            CaptureAttempt(watcher.Latest);
            SaveRun(watcher.Latest);
        }

        void CaptureAttempt(ReaderSample s)
        {
            attemptMap = s.Map ?? "";
            attemptProcess = s.ProcessId;
            attemptLevel = s.LevelId;
            attemptProcessStart = ProcessStartTicks(s.ProcessId);
            lastClockSeen = s.Clock;
        }

        static long ProcessStartTicks(uint processId)
        {
            if (processId == 0) return 0;
            try
            {
                using (Process process = Process.GetProcessById((int)processId)) return process.StartTime.ToUniversalTime().Ticks;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        void SaveRun(ReaderSample sample)
        {
            if (!Active || attemptProcess == 0) return;
            lastSave = DateTime.UtcNow;
            bool paused = state.CurrentPhase == TimerPhase.Paused;
            store.Save(new SavedRun
            {
                Identity = logic.Identity,
                Map = attemptMap,
                ProcessId = attemptProcess,
                ProcessStartTicks = attemptProcessStart,
                LevelId = attemptLevel,
                StartClock = startClock.Value,
                BaseTicks = baseTicks,
                LastClock = lastClockSeen,
                AttemptStartedUtc = attemptStartUtc.Ticks,
                Segments = state.Run.Count,
                Paused = paused,
                PausedAtTicks = paused ? state.TimePausedAt.Ticks : 0,
                Splits = new List<SavedSplit>(madeSplits),
                LogicState = logic.SaveState(),
            });
        }

        void Housekeeping()
        {
            if (state.CurrentPhase == TimerPhase.Ended && inactiveSince == null) inactiveSince = DateTime.UtcNow;
            bool idle = state.CurrentPhase == TimerPhase.NotRunning || state.CurrentPhase == TimerPhase.Ended;
            if (inactiveSince.HasValue && idle && DateTime.UtcNow - inactiveSince.Value > TimeSpan.FromSeconds(1))
            {
                inactiveSince = null;
                if (DateTime.UtcNow > closingUntil) store.Delete();
            }
        }

        void TryResume(ReaderSample sample)
        {
            if (sample.Status < ReaderStatus.Ready || sample.ProcessId == 0) return;
            long processStart = ProcessStartTicks(sample.ProcessId);
            if (!saved.Matches(sample, processStart, logic.Identity, state.Run.Count))
            {
                store.Delete();
                saved = null;
                return;
            }
            SavedRun run = saved;
            saved = null;
            var started = new DateTime(run.AttemptStartedUtc, DateTimeKind.Utc);
            TimeSpan elapsed = run.Paused ? TimeSpan.FromTicks(run.PausedAtTicks) : DateTime.UtcNow - started;
            timer.Start();
            timer.InitializeGameTime();
            state.IsGameTimePaused = true;
            state.StartTime -= elapsed;
            state.AdjustedStartTime -= elapsed;
            state.StartTimeWithOffset -= elapsed;
            state.AttemptStarted = new AtomicDateTime(started, false);

            int index = 0;
            foreach (SavedSplit split in run.Splits)
            {
                if (index >= state.Run.Count - 1) break;
                state.Run[index++].SplitTime = new Time(split.RealTicks.HasValue ? TimeSpan.FromTicks(split.RealTicks.Value) : (TimeSpan?)null,
                                                       split.GameTicks.HasValue ? TimeSpan.FromTicks(split.GameTicks.Value) : (TimeSpan?)null);
            }
            madeSplits.Clear();
            madeSplits.AddRange(run.Splits.Take(index));
            int skip = logic.Resume(run.LogicState, run.Splits.Select(s => s.Label).ToList(), sample);
            for (int i = 0; i < skip && index < state.Run.Count - 1; i++)
            {
                state.Run[index++].SplitTime = default(Time);
                madeSplits.Add(new SavedSplit { Label = "(skipped)" });
            }
            state.CurrentSplitIndex = index;

            startClock = run.StartClock;
            startGeneration = sample.Generation;
            baseTicks = run.BaseTicks;
            attemptStartUtc = started;
            CaptureAttempt(sample);
            inactiveSince = null;
            pending.Clear();
            state.SetGameTime(Ticks(baseTicks + sample.Clock - startClock.Value));
            if (run.Paused) timer.Pause();
            SaveRun(sample);
        }

        static TimeSpan Ticks(uint ticks) => TimeSpan.FromTicks(Tick.Ticks * ticks);

        string Status(ReaderSample s)
        {
            if (watcher.Error != null) return "reader error: " + watcher.Error;
            switch (s.Status)
            {
                case ReaderStatus.NoProcess: return "waiting for game";
                case ReaderStatus.Unsupported: return "unsupported game build";
                case ReaderStatus.NoLevel: return Running ? "no level: game time paused" : "no level (menu or client)";
                case ReaderStatus.Resolving: return $"{Map(s)} resolving";
                default:
                    string round = logic.Profile == ReaderProfile.Rounds && s.Known(1) ? $"round {s.Values[1]}, " : "";
                    return $"{Map(s)} {round}tick {s.Clock}";
            }
        }

        static string Map(ReaderSample s) => string.IsNullOrEmpty(s.Map) ? "map ?" : s.Map;

        string Next()
        {
            switch (state.CurrentPhase)
            {
                case TimerPhase.NotRunning:
                    ReaderSample latest = watcher.Latest;
                    if (latest.Status >= ReaderStatus.Resolving && !string.IsNullOrEmpty(latest.Map) && !logic.SupportsMap(latest.Map))
                        return $"waiting for start: {latest.Map} not supported";
                    if (latest.Status >= ReaderStatus.Resolving && logic.StartBlocked is string blocked)
                        return $"waiting for start: {blocked}";
                    return "start: player control";
                case TimerPhase.Running:
                case TimerPhase.Paused: return startClock.HasValue ? logic.Next : "started manually, no game time";
                default: return lastSplit != null ? "done (" + lastSplit + ")" : "done";
            }
        }

        void Prepare(InfoTextComponent row, LiveSplitState state, LayoutMode mode)
        {
            row.NameLabel.HasShadow = row.ValueLabel.HasShadow = state.LayoutSettings.DropShadows;
            row.NameLabel.ShadowColor = row.ValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            row.NameLabel.OutlineColor = row.ValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            row.NameLabel.ForeColor = row.ValueLabel.ForeColor = state.LayoutSettings.TextColor;
            row.PrepareDraw(state, mode);
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
            float y = 0;
            foreach (InfoTextComponent row in Rows)
            {
                var transform = g.Transform;
                g.TranslateTransform(0, y);
                Prepare(row, state, LayoutMode.Vertical);
                row.DrawVertical(g, state, width, clipRegion);
                g.Transform = transform;
                y += row.VerticalHeight;
            }
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
            float x = 0;
            foreach (InfoTextComponent row in Rows)
            {
                var transform = g.Transform;
                g.TranslateTransform(x, 0);
                Prepare(row, state, LayoutMode.Horizontal);
                row.DrawHorizontal(g, state, height, clipRegion);
                g.Transform = transform;
                x += row.HorizontalWidth;
            }
        }

        public Control GetSettingsControl(LayoutMode mode) => settings;
        public XmlNode GetSettings(XmlDocument document) => settings.WriteXml(document);
        public void SetSettings(XmlNode settings) => this.settings.ReadXml(settings);

        public void Dispose()
        {
            state.OnReset -= OnReset;
            watcher.Dispose();
            settings.Dispose();
        }
    }
}
