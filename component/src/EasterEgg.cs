using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct EeStep
    {
        public EeStep(string id, string label, string trigger, bool forced = false)
        {
            Id = id;
            Label = label;
            Trigger = trigger;
            Forced = forced;
        }

        public string Id { get; }
        public string Label { get; }
        public string Trigger { get; }
        public bool Forced { get; }
    }

    internal sealed class EeMap
    {
        readonly bool lastForced;

        public EeMap(string script, string name, IReadOnlyList<EeStep> solo, IReadOnlyList<EeStep> coop = null, bool lastForced = true,
            string branch = null, IReadOnlyList<EeMap> branches = null)
        {
            Script = script;
            Name = name;
            Solo = solo;
            Coop = coop ?? solo;
            this.lastForced = lastForced;
            Branch = branch;
            Branches = branches;
        }

        public string Branch { get; }
        public IReadOnlyList<EeMap> Branches { get; }
        public IReadOnlyList<EeMap> Parts => Branches ?? new[] { this };
        public string Key => Script + (Branch != null ? "_" + Branch.ToLowerInvariant() : "");

        public string Script { get; }
        public string Name { get; }
        public IReadOnlyList<EeStep> Solo { get; }
        public IReadOnlyList<EeStep> Coop { get; }
        public bool Differs => !ReferenceEquals(Solo, Coop);

        public IReadOnlyList<EeStep> Steps(bool coop) => coop ? Coop : Solo;
        public uint Every(bool coop) => (uint)((1UL << Steps(coop).Count) - 1);

        public uint ForcedMask(bool coop, bool super)
        {
            if (super && Script != "zm_buried") return 0;
            IReadOnlyList<EeStep> steps = Steps(coop);
            uint mask = lastForced ? 1u << (steps.Count - 1) : 0;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].Forced) mask |= 1u << i;
            return mask;
        }
    }

    internal static class EeMaps
    {
        static EeStep[] Steps(params EeStep[] steps) => steps;

        static readonly EeStep Symbols = new EeStep("highrise_symbols", "Symbols", "flag sq_atd_drg_puzzle_complete");
        static readonly EeStep Perks = new EeStep("highrise_perks", "High Maintenance",
            "first +1 of players[0].num_perks after flag sq_tower_active that is not a purchase, a revive return or a repeat");
        static readonly EeStep Plane1 = new EeStep("plane_1", "Flight 1", "flag plane_boarded, 1st rise");
        static readonly EeStep Plane2 = new EeStep("plane_2", "Flight 2", "flag plane_boarded, 2nd rise");
        static readonly EeStep Plane3 = new EeStep("plane_3", "Flight 3", "flag plane_boarded, 3rd rise");
        static readonly EeStep Codes = new EeStep("codes", "Codes", "4th rise of flag nixie_ee_flashing once level.a_important_codes grew by 4");
        static readonly EeStep Cipher = new EeStep("cipher", "Cipher", "buildable buried_sq_oillamp built");
        static readonly EeStep TimeTravel = new EeStep("time_travel", "Time Travel", "flag sq_tpo_special_round_active or sq_wisp_saved_with_time_bomb");
        static readonly EeStep Sharpshooter = new EeStep("sharpshooter", "Sharpshooter", "level.buried_sq_maxis_complete or _richtofen_complete");
        static readonly EeStep MinedGames = new EeStep("mined_games", "Mined Games", "level.metagame_sq_maxis_complete or _richtofen_complete (Super only)");
        static readonly EeStep Boxes = new EeStep("boxes", "Chests Filled", "level.n_soul_boxes_completed reaches 4, split 4.3 s later");
        static readonly EeStep AscendFromDarkness = new EeStep("AFD", "Ascend from Darkness", "flag ee_all_staffs_placed");
        static readonly EeStep Freedom = new EeStep("freedom", "Freedom", "level.intermission rises after flag ee_samantha_released");

        static readonly EeMap TransitRichtofen = new EeMap("zm_transit", "TranZit", Steps(
            new EeStep("jetgun_power_off", "Jetgun", "after flag power_on: sq_progress[rich][A_jetgun_built]"),
            new EeStep("tower", "Tower", "sq_progress[rich][A_jetgun_tower]"),
            new EeStep("EMP", "EMP", "sq_progress[rich][FINISHED]; the last step")), branch: "Richtofen");
        static readonly EeMap TransitMaxis = new EeMap("zm_transit", "TranZit", Steps(
            new EeStep("jetgun_power_off", "Power off", "after flag power_on: level.power_event_in_progress rising"),
            new EeStep("tower", "Turbines", "sq_progress[maxis][FINISHED]; the last step")), branch: "Maxis");
        static readonly EeMap Transit = new EeMap("zm_transit", "TranZit", TransitRichtofen.Solo, branches: new[] { TransitRichtofen, TransitMaxis });

        static readonly EeMap DieRise = new EeMap("zm_highrise", "Die Rise", Steps(Symbols, Perks));

        public static readonly EeMap[] All =
        {
            Transit,
            DieRise,
            new EeMap("zm_prison", "Mob of the Dead",
                solo: Steps(
                    new EeStep("dryer", "Dryer", "flag dryer_cycle_active"),
                    new EeStep("gondola_1", "Gondola 1", "flag gondola_in_motion, 1st rise after gondola_initialized"),
                    Plane1,
                    new EeStep("gondola_2", "Gondola 2", "flag gondola_in_motion, 2nd rise"),
                    Plane2,
                    new EeStep("gondola_3", "Gondola 3", "flag gondola_in_motion, 3rd rise"),
                    Plane3,
                    Codes,
                    new EeStep("headphones", "Headphones", "level.m_headphones deleted (the last audio log has ended)")),
                coop: Steps(Plane1, Plane2, Plane3, Codes,
                    new EeStep("fight", "Showdown", "level.winner is set, split 2 s later"))),
            new EeMap("zm_buried", "Buried",
                solo: Steps(new EeStep("boxhit", "Boxhit", "flag chest_has_been_used"),
                    new EeStep("ghosts", "Ghosts", "flag spawn_ghosts"), Cipher, TimeTravel, Sharpshooter),
                coop: Steps(Cipher, TimeTravel, Sharpshooter)),
            new EeMap("zm_tomb", "Origins",
                solo: Steps(new EeStep("NML", "No Man's Land", "flag activate_zone_nml"), Boxes,
                    new EeStep("staff_1", "Staff 1", "level.n_staffs_crafted reaches 1"),
                    new EeStep("staff_2", "Staff 2", "level.n_staffs_crafted reaches 2"),
                    new EeStep("staff_3", "Staff 3", "level.n_staffs_crafted reaches 3"),
                    new EeStep("staff_4", "Staff 4", "level.n_staffs_crafted reaches 4"),
                    AscendFromDarkness,
                    new EeStep("rain_fire", "Rain Fire", "flag ee_mech_zombie_hole_opened"),
                    Freedom),
                coop: Steps(Boxes, AscendFromDarkness, Freedom)),
        };

        public static readonly EeMap[] Super =
        {
            Transit,
            DieRise,
            new EeMap("zm_buried", "Buried",
                solo: Steps(All[3].Solo.Concat(new[] { MinedGames }).ToArray()),
                coop: Steps(All[3].Coop.Concat(new[] { MinedGames }).ToArray())),
        };

        public static EeMap For(string script, bool super) => (super ? Super : All).FirstOrDefault(m => m.Script == script);
    }

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

    internal abstract class EeDetector
    {
        protected static bool Rose(GameEvent e) => e.OldValue == 0 && e.NewValue != 0;

        public abstract IEnumerable<EeHit> OnChange(GameEvent e);

        public static EeDetector Create(string script)
        {
            switch (script)
            {
                case "zm_transit": return new TransitDetector();
                case "zm_highrise": return new HighriseDetector();
                case "zm_prison": return new PrisonDetector();
                case "zm_buried": return new BuriedDetector();
                case "zm_tomb": return new TombDetector();
                default: return null;
            }
        }
    }

    internal enum TransitSide { Unknown, Richtofen, Maxis }

    internal sealed class TransitDetector : EeDetector
    {
        bool powerSeen, jetgunBuilt;

        public TransitSide Side { get; private set; }

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case 2:
                    if (Rose(e)) powerSeen = true;
                    break;
                case 3:
                    if (Rose(e) && powerSeen && Side == TransitSide.Unknown)
                    {
                        Side = TransitSide.Maxis;
                        yield return new EeHit("jetgun_power_off", e.Clock);
                    }
                    break;
                case 4:
                    if (Rose(e)) jetgunBuilt = true;
                    break;
                case 5:
                    if (Rose(e) && Side == TransitSide.Richtofen) yield return new EeHit("tower", e.Clock);
                    break;
                case 7:
                    if (Rose(e) && Side == TransitSide.Richtofen) yield return new EeHit("EMP", e.Clock);
                    break;
                case 8:
                    if (Rose(e) && Side == TransitSide.Maxis) yield return new EeHit("tower", e.Clock);
                    break;
            }
            if (Side == TransitSide.Unknown && powerSeen && jetgunBuilt)
            {
                Side = TransitSide.Richtofen;
                yield return new EeHit("jetgun_power_off", e.Clock);
            }
        }
    }

    internal sealed class HighriseDetector : EeDetector
    {
        const uint PurchaseTicks = 65;
        const uint RecentTicks = 2;
        const uint RepeatTicks = 5;

        bool towerActive, marker, laststand, revive;
        uint? markerSetAt, markerClearedAt, standingClearedAt, lastRise;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case 1:
                    if (Rose(e)) yield return new EeHit("highrise_symbols", e.Clock);
                    break;
                case 2:
                    towerActive = e.NewValue != 0;
                    break;
                case 4:
                    marker = e.NewValue != 0;
                    if (marker) markerSetAt = e.Clock;
                    else markerClearedAt = e.Clock;
                    break;
                case 5:
                case 7:
                    bool was = laststand || revive;
                    if (e.Watch == 5) laststand = e.NewValue != 0;
                    else revive = e.NewValue != 0;
                    if (was && !(laststand || revive)) standingClearedAt = e.Clock;
                    break;
                case 3:
                    if (e.NewValue <= e.OldValue) break;
                    bool purchase = (marker && markerSetAt.HasValue && e.Clock - markerSetAt.Value <= PurchaseTicks)
                                    || (markerClearedAt.HasValue && e.Clock - markerClearedAt.Value <= RecentTicks);
                    bool returned = laststand || revive || (standingClearedAt.HasValue && e.Clock - standingClearedAt.Value <= RecentTicks);
                    bool repeat = lastRise.HasValue && e.Clock - lastRise.Value <= RepeatTicks;
                    lastRise = e.Clock;
                    if (towerActive && e.NewValue - e.OldValue == 1 && !purchase && !returned && !repeat)
                        yield return new EeHit("highrise_perks", e.Clock);
                    break;
            }
        }
    }

    internal sealed class PrisonDetector : EeDetector
    {
        const uint ShowdownTicks = 40;

        bool gondolaReady, codesStage;
        int gondolas, planes, finals;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case 1:
                    if (Rose(e)) yield return new EeHit("dryer", e.Clock);
                    break;
                case 2:
                    if (e.NewValue != 0) gondolaReady = true;
                    break;
                case 3:
                    if (Rose(e) && gondolaReady && ++gondolas <= 3) yield return new EeHit("gondola_" + gondolas, e.Clock);
                    break;
                case 4:
                    if (Rose(e) && ++planes <= 3) yield return new EeHit("plane_" + planes, e.Clock);
                    break;
                case 7:
                    if (e.NewValue - e.OldValue >= 4) codesStage = true;
                    break;
                case 5:
                    if (Rose(e) && codesStage && ++finals == 4) yield return new EeHit("codes", e.Clock);
                    break;
                case 8:
                    if (e.OldValue == 1 && e.NewValue != 1) yield return new EeHit("headphones", e.Clock);
                    break;
                case 9:
                    if (Rose(e)) yield return new EeHit("fight", e.Clock + ShowdownTicks);
                    break;
            }
        }
    }

    internal sealed class BuriedDetector : EeDetector
    {
        const int OilLampBit = 6;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case 2:
                    if (Rose(e)) yield return new EeHit("boxhit", e.Clock);
                    break;
                case 3:
                    if (Rose(e)) yield return new EeHit("ghosts", e.Clock);
                    break;
                case 4:
                case 5:
                    if (Rose(e)) yield return new EeHit("time_travel", e.Clock);
                    break;
                case 7:
                case 8:
                    if (Rose(e)) yield return new EeHit("sharpshooter", e.Clock);
                    break;
                case 9:
                case 10:
                    if (Rose(e)) yield return new EeHit("mined_games", e.Clock);
                    break;
                case 11:
                    if ((e.OldValue & (1 << OilLampBit)) == 0 && (e.NewValue & (1 << OilLampBit)) != 0) yield return new EeHit("cipher", e.Clock);
                    break;
            }
        }
    }

    internal sealed class TombDetector : EeDetector
    {
        const uint BoxesTicks = 86;

        bool released;

        public override IEnumerable<EeHit> OnChange(GameEvent e)
        {
            switch (e.Watch)
            {
                case 1:
                    if (Rose(e)) yield return new EeHit("NML", e.Clock);
                    break;
                case 2:
                    if (e.OldValue < 4 && e.NewValue >= 4) yield return new EeHit("boxes", e.Clock + BoxesTicks);
                    break;
                case 3:
                    for (int staff = Math.Max(e.OldValue + 1, 1); staff <= Math.Min(e.NewValue, 4); staff++)
                        yield return new EeHit("staff_" + staff, e.Clock);
                    break;
                case 4:
                    if (Rose(e)) yield return new EeHit("AFD", e.Clock);
                    break;
                case 5:
                    if (Rose(e)) yield return new EeHit("rain_fire", e.Clock);
                    break;
                case 7:
                    if (e.NewValue != 0) released = true;
                    break;
                case 8:
                    if (Rose(e) && released) yield return new EeHit("freedom", e.Clock);
                    break;
            }
        }
    }

    internal sealed class EasterEggLogic : RunLogic
    {
        const string TransitScript = "zm_transit";
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
            classic = map != TransitScript || (s.Known(1) && s.Values[1] != 0);
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

        uint Mask(EeMap part, bool players) => (chosen(part, players) & part.Every(players)) | part.ForcedMask(players, super);

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            if (!super)
            {
                if (!running)
                {
                    if (BlackscreenEnded(e) && SupportsMap(map) && classic)
                    {
                        detector = EeDetector.Create(map);
                        yield return StartRun(e.Clock);
                    }
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
                IReadOnlyList<EeStep> steps = part.Steps(players);
                uint mask = Mask(part, players);
                int index = IndexOf(steps, hit.Id);
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

        static int IndexOf(IReadOnlyList<EeStep> steps, string id)
        {
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].Id == id) return i;
            return -1;
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
                IReadOnlyList<EeStep> steps = part.Steps(players);
                for (int i = 0; i < steps.Count; i++)
                    if ((mask & (1u << i)) != 0 && !done.Contains(map + ":" + steps[i].Id))
                        return (super ? eeMap.Name + ": " : "") + steps[i].Label;
                return "done";
            }
        }
    }
}
