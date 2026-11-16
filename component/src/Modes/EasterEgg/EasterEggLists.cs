using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class EeStep : Step
    {
        public EeStep(string id, string label) : base(id, label) { }
    }

    internal sealed class EeMap
    {
        readonly StepList<EeStep>[] lists;

        public EeMap(string script, IReadOnlyList<EeStep> solo, IReadOnlyList<EeStep> coop = null,
            string branch = null, IReadOnlyList<EeMap> branches = null)
        {
            Script = script;
            Solo = solo;
            Coop = coop ?? solo;
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

        public StepList<EeStep> List(bool coop, bool super) => lists[(super ? 2 : 0) + (coop ? 1 : 0)];

        StepList<EeStep> Build(bool coop, bool super)
        {
            IReadOnlyList<EeStep> steps = coop ? Coop : Solo;
            string key = (super ? "super_" : "") + Key + (coop ? "_coop" : "_solo");
            return new StepList<EeStep>(key, Script, steps, forced: ForcedMask(steps, super));
        }

        uint ForcedMask(IReadOnlyList<EeStep> steps, bool super) => super && Script != "zm_buried" ? 0 : 1u << (steps.Count - 1);
    }

    internal static class EeMaps
    {
        static EeStep[] Steps(params EeStep[] steps) => steps;

        static readonly EeStep Symbols = new EeStep("highrise_symbols", "Symbols");
        static readonly EeStep Perks = new EeStep("highrise_perks", "High Maintenance");
        static readonly EeStep Plane1 = new EeStep("plane_1", "Plane I");
        static readonly EeStep Plane2 = new EeStep("plane_2", "Plane II");
        static readonly EeStep Plane3 = new EeStep("plane_3", "Plane III");
        static readonly EeStep Codes = new EeStep("codes", "Codes");
        static readonly EeStep Cipher = new EeStep("cipher", "Cipher");
        static readonly EeStep TimeTravel = new EeStep("time_travel", "Time Travel");
        static readonly EeStep Sharpshooter = new EeStep("sharpshooter", "Sharpshooter");
        static readonly EeStep MinedGames = new EeStep("mined_games", "Mined Games");
        static readonly EeStep Boxes = new EeStep("boxes", "Boxes");
        static readonly EeStep AscendFromDarkness = new EeStep("AFD", "AFD");
        static readonly EeStep Freedom = new EeStep("freedom", "Freedom");

        static readonly EeMap TransitRichtofen = new EeMap("zm_transit", Steps(
            new EeStep("jetgun_power_off", "Jetgun"),
            new EeStep("tower", "Tower"),
            new EeStep("EMP", "Lights")), branch: "Richtofen");
        static readonly EeMap TransitMaxis = new EeMap("zm_transit", Steps(
            new EeStep("jetgun_power_off", "Power off"),
            new EeStep("tower", "Turbines")), branch: "Maxis");
        static readonly EeMap Transit = new EeMap("zm_transit", TransitRichtofen.Solo, branches: new[] { TransitRichtofen, TransitMaxis });

        static readonly EeMap DieRise = new EeMap("zm_highrise", Steps(Symbols, Perks));

        static readonly EeStep Boxhit = new EeStep("boxhit", "Boxhit");
        static readonly EeStep Ghosts = new EeStep("ghosts", "Ghosts");
        static readonly EeStep Levers = new EeStep("bells_levers", "Levers");
        static readonly EeStep Bells = new EeStep("bells_levers", "Bells");
        static readonly EeMap BuriedRichtofen = new EeMap("zm_buried",
            solo: Steps(Boxhit, Ghosts, Cipher, TimeTravel, Levers, Sharpshooter),
            coop: Steps(Cipher, TimeTravel, Levers, Sharpshooter), branch: "Richtofen");
        static readonly EeMap BuriedMaxis = new EeMap("zm_buried",
            solo: Steps(Boxhit, Ghosts, Cipher, TimeTravel, Bells, Sharpshooter),
            coop: Steps(Cipher, TimeTravel, Bells, Sharpshooter), branch: "Maxis");
        static readonly EeMap Buried = new EeMap("zm_buried", BuriedRichtofen.Solo, BuriedRichtofen.Coop,
            branches: new[] { BuriedRichtofen, BuriedMaxis });

        static EeMap WithMinedGames(EeMap part) => new EeMap(part.Script,
            Steps(part.Solo.Concat(new[] { MinedGames }).ToArray()), Steps(part.Coop.Concat(new[] { MinedGames }).ToArray()),
            branch: part.Branch);
        static readonly EeMap SuperBuriedRichtofen = WithMinedGames(BuriedRichtofen);
        static readonly EeMap SuperBuriedMaxis = WithMinedGames(BuriedMaxis);

        public static readonly EeMap[] All =
        {
            Transit,
            DieRise,
            new EeMap("zm_prison",
                solo: Steps(
                    new EeStep("dryer", "Dryer"),
                    new EeStep("gondola_1", "Gondola I"),
                    Plane1,
                    new EeStep("gondola_2", "Gondola II"),
                    Plane2,
                    new EeStep("gondola_3", "Gondola III"),
                    Plane3,
                    Codes,
                    new EeStep("headphones", "Headphones")),
                coop: Steps(Plane1, Plane2, Plane3, Codes,
                    new EeStep("fight", "Showdown"))),
            Buried,
            new EeMap("zm_tomb",
                solo: Steps(new EeStep("NML", "NML"), Boxes,
                    new EeStep("staff_1", "Staff I"),
                    new EeStep("staff_2", "Staff II"),
                    new EeStep("staff_3", "Staff III"),
                    new EeStep("staff_4", "Staff IV"),
                    AscendFromDarkness,
                    new EeStep("rain_fire", "Rain Fire"),
                    Freedom),
                coop: Steps(Boxes, AscendFromDarkness, Freedom)),
        };

        public static readonly EeMap[] Super =
        {
            Transit,
            DieRise,
            new EeMap("zm_buried", SuperBuriedRichtofen.Solo, SuperBuriedRichtofen.Coop,
                branches: new[] { SuperBuriedRichtofen, SuperBuriedMaxis }),
        };

        public static EeMap For(string script, bool super) => (super ? Super : All).FirstOrDefault(m => m.Script == script);
    }
}
