using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class EeStep : Step
    {
        public EeStep(string id, string label, string trigger, bool forced = false) : base(id, label, trigger) => Forced = forced;

        public bool Forced { get; }
    }

    internal sealed class EeMap
    {
        readonly bool lastForced;
        readonly StepList<EeStep>[] lists;

        public EeMap(string script, IReadOnlyList<EeStep> solo, IReadOnlyList<EeStep> coop = null, bool lastForced = true,
            string branch = null, IReadOnlyList<EeMap> branches = null)
        {
            Script = script;
            Solo = solo;
            Coop = coop ?? solo;
            this.lastForced = lastForced;
            Branch = branch;
            Branches = branches;
            lists = new[] { Build(false, false), Build(true, false), Build(false, true), Build(true, true) };
        }

        public string Branch { get; }
        public IReadOnlyList<EeMap> Branches { get; }
        public IReadOnlyList<EeMap> Parts => Branches ?? new[] { this };
        public string Key => Script + (Branch != null ? "_" + Branch.ToLowerInvariant() : "");

        public string Script { get; }
        public string Name => GameMaps.Name(Script);
        public IReadOnlyList<EeStep> Solo { get; }
        public IReadOnlyList<EeStep> Coop { get; }
        public bool Differs => !ReferenceEquals(Solo, Coop);

        public StepList<EeStep> List(bool coop, bool super) => lists[(super ? 2 : 0) + (coop ? 1 : 0)];

        StepList<EeStep> Build(bool coop, bool super)
        {
            IReadOnlyList<EeStep> steps = coop ? Coop : Solo;
            string key = (super ? "super_" : "") + Key + (coop ? "_coop" : "_solo");
            return new StepList<EeStep>(key, Script, steps, forced: ForcedMask(steps, super));
        }

        uint ForcedMask(IReadOnlyList<EeStep> steps, bool super)
        {
            if (super && Script != "zm_buried") return 0;
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
            "the first reward orb is deleted (Plutonium); without an orb seen, the first +1 of players[0].num_perks after flag sq_tower_active that is not a purchase, a revive return or a repeat");
        static readonly EeStep Plane1 = new EeStep("plane_1", "Plane I", "flag plane_boarded, 1st rise");
        static readonly EeStep Plane2 = new EeStep("plane_2", "Plane II", "flag plane_boarded, 2nd rise");
        static readonly EeStep Plane3 = new EeStep("plane_3", "Plane III", "flag plane_boarded, 3rd rise");
        static readonly EeStep Codes = new EeStep("codes", "Codes", "4th rise of flag nixie_ee_flashing once level.a_important_codes grew by 4");
        static readonly EeStep Cipher = new EeStep("cipher", "Cipher", "buildable buried_sq_oillamp built");
        static readonly EeStep TimeTravel = new EeStep("time_travel", "Time Travel", "flag sq_tpo_special_round_active or sq_wisp_saved_with_time_bomb");
        static readonly EeStep Sharpshooter = new EeStep("sharpshooter", "Sharpshooter", "level.buried_sq_maxis_complete or _richtofen_complete");
        static readonly EeStep MinedGames = new EeStep("mined_games", "Mined Games", "level.metagame_sq_maxis_complete or _richtofen_complete (Super only)");
        static readonly EeStep Boxes = new EeStep("boxes", "Boxes", "level.n_soul_boxes_completed reaches 4, split 4.3 s later");
        static readonly EeStep AscendFromDarkness = new EeStep("AFD", "AFD", "flag ee_all_staffs_placed");
        static readonly EeStep Freedom = new EeStep("freedom", "Freedom", "level.intermission rises after flag ee_samantha_released");

        static readonly EeMap TransitRichtofen = new EeMap("zm_transit", Steps(
            new EeStep("jetgun_power_off", "Jetgun", "after flag power_on: sq_progress[rich][A_jetgun_built]"),
            new EeStep("tower", "Tower", "sq_progress[rich][A_jetgun_tower]"),
            new EeStep("EMP", "Lights", "sq_progress[rich][FINISHED]; the last step")), branch: "Richtofen");
        static readonly EeMap TransitMaxis = new EeMap("zm_transit", Steps(
            new EeStep("jetgun_power_off", "Power off", "after flag power_on: level.power_event_in_progress rising"),
            new EeStep("tower", "Turbines", "sq_progress[maxis][FINISHED]; the last step")), branch: "Maxis");
        static readonly EeMap Transit = new EeMap("zm_transit", TransitRichtofen.Solo, branches: new[] { TransitRichtofen, TransitMaxis });

        static readonly EeMap DieRise = new EeMap("zm_highrise", Steps(Symbols, Perks));

        public static readonly EeMap[] All =
        {
            Transit,
            DieRise,
            new EeMap("zm_prison",
                solo: Steps(
                    new EeStep("dryer", "Dryer", "flag dryer_cycle_active"),
                    new EeStep("gondola_1", "Gondola I", "flag gondola_in_motion, 1st rise after gondola_initialized"),
                    Plane1,
                    new EeStep("gondola_2", "Gondola II", "flag gondola_in_motion, 2nd rise"),
                    Plane2,
                    new EeStep("gondola_3", "Gondola III", "flag gondola_in_motion, 3rd rise"),
                    Plane3,
                    Codes,
                    new EeStep("headphones", "Headphones", "level.m_headphones deleted (the last audio log has ended)")),
                coop: Steps(Plane1, Plane2, Plane3, Codes,
                    new EeStep("fight", "Showdown", "level.winner is set, split 2 s later"))),
            new EeMap("zm_buried",
                solo: Steps(new EeStep("boxhit", "Boxhit", "flag chest_has_been_used"),
                    new EeStep("ghosts", "Ghosts", "flag spawn_ghosts"), Cipher, TimeTravel, Sharpshooter),
                coop: Steps(Cipher, TimeTravel, Sharpshooter)),
            new EeMap("zm_tomb",
                solo: Steps(new EeStep("NML", "NML", "flag activate_zone_nml"), Boxes,
                    new EeStep("staff_1", "Staff I", "level.n_staffs_crafted reaches 1"),
                    new EeStep("staff_2", "Staff II", "level.n_staffs_crafted reaches 2"),
                    new EeStep("staff_3", "Staff III", "level.n_staffs_crafted reaches 3"),
                    new EeStep("staff_4", "Staff IV", "level.n_staffs_crafted reaches 4"),
                    AscendFromDarkness,
                    new EeStep("rain_fire", "Rain Fire", "flag ee_mech_zombie_hole_opened"),
                    Freedom),
                coop: Steps(Boxes, AscendFromDarkness, Freedom)),
        };

        public static readonly EeMap[] Super =
        {
            Transit,
            DieRise,
            new EeMap("zm_buried",
                solo: Steps(All[3].Solo.Concat(new[] { MinedGames }).ToArray()),
                coop: Steps(All[3].Coop.Concat(new[] { MinedGames }).ToArray())),
        };

        public static EeMap For(string script, bool super) => (super ? Super : All).FirstOrDefault(m => m.Script == script);
    }
}
