using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct EeHit
    {
        public EeHit(string id, uint tick)
        {
            Id = id;
            Tick = tick;
        }

        public string Id { get; }
        public uint Tick { get; }
    }

    internal sealed class EasterEggLogic : RunLogic
    {
        const string TransitScript = "zm_transit";
        const int ClassicWatch = 1;
        const int SuperMaps = 3;

        readonly bool super;
        readonly Func<bool> coop;
        readonly Func<EeMap, bool, uint> chosen;
        readonly Func<bool> pauseBetween;
        readonly HashSet<string> done = new HashSet<string>();

        string map = "";
        bool classic = true;
        EeDetector detector;
        uint resetGeneration = uint.MaxValue;
        uint classicGeneration = uint.MaxValue;
        int completed;
        uint carry;
        bool attempt;
        uint attemptStart, baseTicks, attemptGeneration, lastGame;

        public EasterEggLogic(bool super, Func<bool> coop, Func<EeMap, bool, uint> chosen, Func<bool> pauseBetween = null)
        {
            this.super = super;
            this.coop = coop;
            this.chosen = chosen;
            this.pauseBetween = pauseBetween ?? (() => false);
        }

        public override ReaderProfile Profile => ReaderProfile.EasterEgg;

        public override bool Continues => super;

        public override uint? FrozenGameTicks => super && !attempt && completed > 0 ? carry : (uint?)null;

        public override bool SupportsMap(string script) => EeMaps.For(script, super) != null;

        public override string StartBlocked
        {
            get
            {
                if (map == TransitScript && !classic) return "only classic TranZit is timed";
                if (super && map != TransitScript) return "load TranZit to start a Super run";
                return null;
            }
        }

        public override bool OnSample(ReaderSample s)
        {
            if (s.Status < ReaderStatus.Resolving) return false;
            if (!string.IsNullOrEmpty(s.Map)) map = s.Map;
            if (map != TransitScript) classic = true;
            else if (s.Known(ClassicWatch))
            {
                classic = s.Values[ClassicWatch] != 0;
                classicGeneration = s.Generation;
            }
            else if (s.Generation != classicGeneration) classic = false;
            if (!super) return false;
            if (attempt && s.Generation == attemptGeneration) lastGame = baseTicks + (s.Clock - attemptStart);
            if (map == TransitScript && classic && s.Generation != resetGeneration)
            {
                resetGeneration = s.Generation;
                ResetChain();
                return true;
            }
            return false;
        }

        void ResetChain()
        {
            completed = 0;
            carry = 0;
            attempt = false;
            lastGame = 0;
            detector = null;
            done.Clear();
        }

        public override void TimerReset()
        {
            if (super) ResetChain();
            detector = null;
        }

        protected override void OnNewLevel()
        {
            if (super && attempt)
            {
                carry = lastGame;
                attempt = false;
            }
            detector = null;
        }

        protected override void Reset()
        {
            done.Clear();
            if (!super) detector = EeDetector.Create(map);
        }

        static int SuperIndex(string script)
        {
            switch (script)
            {
                case TransitScript: return 0;
                case "zm_highrise": return 1;
                case "zm_buried": return 2;
                default: return -1;
            }
        }

        EeMap Part(EeMap eeMap) => eeMap.Branches == null ? eeMap : (((detector as TransitDetector)?.Side == TransitSide.Maxis) ? eeMap.Branches[1] : eeMap.Branches[0]);

        uint Mask(EeMap part, bool players)
        {
            StepList<EeStep> list = part.List(players, super);
            return (chosen(part, players) & list.Every) | list.Forced;
        }

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!super)
            {
                if (!running)
                {
                    if (BlackscreenEnded(e) && SupportsMap(map) && classic) yield return StartRun(e.Clock);
                    yield break;
                }
                foreach (TimerAction action in Handle(e)) yield return action;
                yield break;
            }

            if (e.Watch == BlackscreenWatch && Became(e, BlackscreenWatch))
            {
                int index = SuperIndex(map);
                if (index == 0 && classic)
                {
                    ResetChain();
                    Begin(e, 0);
                    yield return new TimerAction(ActionKind.Start, e.Clock, "start", 0);
                }
                else if (index > 0 && running && completed == index)
                {
                    Begin(e, carry);
                    yield return new TimerAction(ActionKind.Rebase, e.Clock, "map " + map, carry);
                }
                yield break;
            }
            if (attempt && e.Generation == attemptGeneration)
                foreach (TimerAction action in Handle(e)) yield return action;
        }

        void Begin(GameEvent e, uint baseTime)
        {
            attempt = true;
            attemptStart = e.Clock;
            baseTicks = baseTime;
            attemptGeneration = e.Generation;
            lastGame = baseTime;
            detector = EeDetector.Create(map);
        }

        IEnumerable<TimerAction> Handle(GameEvent e)
        {
            EeMap eeMap = EeMaps.For(map, super);
            if (detector == null || eeMap == null) yield break;
            bool players = coop();
            foreach (EeHit hit in detector.OnChange(e).ToList())
            {
                EeMap part = Part(eeMap);
                StepList<EeStep> list = part.List(players, super);
                IReadOnlyList<EeStep> steps = list.Steps;
                uint mask = Mask(part, players);
                int index = list.IndexOf(hit.Id);
                bool complete = steps.Count > 0 && steps[steps.Count - 1].Id == hit.Id;
                if (!done.Add(map + ":" + hit.Id)) continue;
                if (index < 0) continue;
                if ((mask & (1u << index)) != 0)
                    yield return new TimerAction(ActionKind.Split, hit.Tick, steps[index].Label, 0);
                if (!complete) continue;

                if (!super) continue;
                carry = baseTicks + (hit.Tick - attemptStart);
                attempt = false;
                completed++;
                if (completed < SuperMaps && pauseBetween())
                    yield return new TimerAction(ActionKind.Pause, hit.Tick, "map complete", 0);
            }
        }

        public override string Next
        {
            get
            {
                EeMap eeMap = EeMaps.For(map, super);
                if (eeMap == null) return super && completed >= SuperMaps ? "done" : "map not supported";
                bool players = coop();
                EeMap part = Part(eeMap);
                uint mask = Mask(part, players);
                IReadOnlyList<EeStep> steps = part.List(players, super).Steps;
                for (int i = 0; i < steps.Count; i++)
                    if ((mask & (1u << i)) != 0 && !done.Contains(map + ":" + steps[i].Id))
                        return (super ? eeMap.Name + ": " : "") + steps[i].Label;
                return "done";
            }
        }
    }
}
