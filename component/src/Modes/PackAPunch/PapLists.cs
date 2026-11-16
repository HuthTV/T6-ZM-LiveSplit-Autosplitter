using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class PapStep : Step
    {
        public PapStep(string label, int watch, int level) : base(label, label)
        {
            Watch = watch;
            Level = level;
        }

        public int Watch { get; }
        public int Level { get; }
    }

    internal sealed class PapMap : StepList<PapStep>
    {
        public PapMap(string key, string script, string name, params PapStep[] steps)
            : base(key, script, steps.Concat(new[] { new PapStep(PackAPunchLogic.PapLabel, PackAPunchLogic.GrabWatch, 1) }).ToArray(), name,
                   forced: 1u << steps.Length) { }

        public override uint Default => Forced;
    }

    internal static class PapMaps
    {
        public const string TownLocation = "town";

        static PapStep[] Rounds(int last) =>
            Enumerable.Range(1, last).Select(k => new PapStep($"End of round {k}", PackAPunchLogic.RoundWatch, k + 1)).ToArray();

        static PapStep[] Parts() =>
            Enumerable.Range(1, 5).Select(n => new PapStep($"Part {n}", PackAPunchLogic.PartsWatch, n)).ToArray();

        static PapStep One(string label, int watch) => new PapStep(label, watch, 1);

        static PapStep[] Join(params IEnumerable<PapStep>[] groups) => groups.SelectMany(g => g).ToArray();

        public static readonly PapMap[] All =
        {
            new PapMap("transit", "zm_transit", "TranZit", One("Power on", PackAPunchLogic.PowerWatch)),
            new PapMap(TownLocation, "zm_transit", "Town", Rounds(5)),
            new PapMap("nuked", "zm_nuked", "Nuketown", Rounds(5)),
            new PapMap("highrise", "zm_highrise", "Die Rise"),
            new PapMap("prison", "zm_prison", "Mob of the Dead",
                Join(new[] { One("Dryer", PackAPunchLogic.DryerWatch) }, Parts(), new[] { One("Plane built", PackAPunchLogic.PlaneWatch) })),
            new PapMap("buried", "zm_buried", "Buried", One("Ghost house", PackAPunchLogic.GhostsWatch)),
            new PapMap("tomb", "zm_tomb", "Origins", Join(Rounds(5), new[] { One("All gens", PackAPunchLogic.GensWatch) })),
        };

        public static bool Supports(string script) => All.Any(m => m.Script == script);

        public static PapMap ByKey(string key) => All.FirstOrDefault(m => m.Key == key);

        public static PapMap For(string script, bool? classic, bool? town)
        {
            if (script != "zm_transit") return All.FirstOrDefault(m => m.Script == script);
            if (classic == true) return ByKey("transit");
            if (town == true) return ByKey(TownLocation);
            return null;
        }
    }
}
