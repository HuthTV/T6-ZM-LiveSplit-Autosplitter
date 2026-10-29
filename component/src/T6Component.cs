using System;
using System.Collections.Generic;
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
        string lastSplit;

        public T6Component(LiveSplitState state)
        {
            this.state = state;
            timer = new TimerModel { CurrentState = state };
            logic = settings.CreateLogic();
            watcher = new Watcher(logic.Profile);
            state.OnReset += OnReset;
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

        void OnReset(object sender, TimerPhase phase)
        {
            startClock = null;
            lastSplit = null;
            pending.Clear();
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
            settings.GameMap = sample.Status >= ReaderStatus.Resolving ? sample.Map : null;
            while (watcher.Events.TryDequeue(out GameEvent e))
            {
                if (e.Kind == GameEventKind.NewLevel)
                {
                    if (state.CurrentPhase != TimerPhase.NotRunning) timer.Reset();
                    pending.Clear();
                    logic.NewLevel();
                    continue;
                }
                if (Running && e.Generation != startGeneration) continue;
                foreach (TimerAction action in logic.OnChange(e, Running))
                {
                    if (action.Start) Start(action, e.Generation);
                    else pending.Enqueue(action);
                }
            }

            if (state.CurrentPhase == TimerPhase.NotRunning && logic.PendingStart is uint due
                && sample.Generation == logic.PendingGeneration && sample.Status >= ReaderStatus.Resolving && sample.Clock >= due)
                Start(logic.StartRun(due), sample.Generation);

            bool live = Running && sample.Generation == startGeneration && sample.Status >= ReaderStatus.NoLevel;
            while (live && Running && pending.Count > 0 && pending.Peek().Tick <= sample.Clock)
            {
                TimerAction split = pending.Dequeue();
                state.SetGameTime(Ticks(split.Tick - startClock.Value));
                timer.Split();
                lastSplit = split.Label;
            }
            if (live && Running)
            {
                state.IsGameTimePaused = true;
                state.SetGameTime(Ticks(sample.Clock - startClock.Value));
            }

            statusRow.InformationValue = Status(sample);
            nextRow.InformationValue = Next();
            foreach (InfoTextComponent row in Rows) row.Update(invalidator, state, width, height, mode);
        }

        void Start(TimerAction action, uint generation)
        {
            if (state.CurrentPhase != TimerPhase.NotRunning) return;
            timer.Start();
            timer.InitializeGameTime();
            state.IsGameTimePaused = true;
            state.SetGameTime(TimeSpan.Zero);
            startClock = action.Tick;
            startGeneration = generation;
            pending.Clear();
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
                    return "start: player control";
                case TimerPhase.Running: return startClock.HasValue ? logic.Next : "started manually, no game time";
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
