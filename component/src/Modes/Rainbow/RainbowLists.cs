using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class RainbowStep : Step
    {
        public RainbowStep(string label, int watch, int level) : base(label, label)
        {
            Watch = watch;
            Level = level;
        }

        public int Watch { get; }
        public int Level { get; }
    }

    internal static class RainbowMaps
    {
        static StepList<RainbowStep> Map(string script, params RainbowStep[] steps) => new StepList<RainbowStep>(script, script, steps);

        public static readonly StepList<RainbowStep>[] All =
        {
            Map("zm_buried",
                Enumerable.Range(1, 7).Select(n => new RainbowStep(n == 1 ? "1 perk" : n + " perks", RainbowLogic.PerksWatch, n)).ToArray()),
            Map("zm_tomb",
                new RainbowStep("Golden Shovel", RainbowLogic.ShovelWatch, 1),
                new RainbowStep("Bottle 1", RainbowLogic.SlotsWatch, 5), new RainbowStep("Bottle 2", RainbowLogic.SlotsWatch, 6),
                new RainbowStep("Bottle 3", RainbowLogic.SlotsWatch, 7), new RainbowStep("Bottle 4", RainbowLogic.SlotsWatch, 8),
                new RainbowStep("8 perks", RainbowLogic.PerksWatch, 8)),
        };

        public static StepList<RainbowStep> For(string script) => All.FirstOrDefault(m => m.Script == script);
    }
}
