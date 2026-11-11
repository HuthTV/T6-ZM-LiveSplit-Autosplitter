using System;

namespace LiveSplit.T6ZM
{
    internal enum Category { Rounds, EasterEgg, SuperEasterEgg, Songs, Rainbow, Buildables, PackAPunch }

    internal sealed class Mode
    {
        readonly Func<ModePage, RunLogic> createLogic;

        Mode(Category category, string name, string description, Type pageType, Func<ModePage, RunLogic> createLogic)
        {
            Category = category;
            Name = name;
            Description = description;
            PageType = pageType;
            this.createLogic = createLogic;
        }

        public static Mode WithPage<TPage>(Category category, string name, string description, Func<TPage, RunLogic> createLogic) where TPage : ModePage =>
            new Mode(category, name, description, typeof(TPage), page => createLogic((TPage)page));

        public static Mode WithoutPage(Category category, string name, string description, Func<RunLogic> createLogic) =>
            new Mode(category, name, description, null, page => createLogic());

        public Category Category { get; }
        public string Name { get; }
        public string Description { get; }
        public Type PageType { get; }

        public RunLogic CreateLogic(ModePage page) => createLogic(page);
    }

    internal static class Modes
    {
        public static readonly Mode[] All =
        {
            Mode.WithPage<RoundsPage>(Category.Rounds, "Round speedrun",
                "Split on selected rounds.",
                page => page.CreateLogic()),
            Mode.WithPage<EasterEggPage>(Category.EasterEgg, "Easter egg",
                "Split on each selected step. The final split is always selected.",
                page => page.CreateLogic(super: false)),
            Mode.WithPage<EasterEggPage>(Category.SuperEasterEgg, "Super Easter egg",
                "The timer continues through TranZit, Die Rise and Buried. In-game time is always paused between maps.",
                page => page.CreateLogic(super: true)),
            Mode.WithPage<SongsPage>(Category.Songs, "Songs",
                "Splits fire in any order.",
                page => page.CreateLogic()),
            Mode.WithPage<RainbowPage>(Category.Rainbow, "Rainbow Perk (solo)",
                "Solo only. The final split is when all perk slots are full.",
                page => page.CreateLogic()),
            Mode.WithPage<BuildablesPage>(Category.Buildables, "Buildables",
                "Splits can be done in any order.",
                page => page.CreateLogic()),
            Mode.WithoutPage(Category.PackAPunch, "Pack-a-Punch",
                "Splits once a player grabs an upgraded weapon from Pack-a-Punch machine.",
                () => new PackAPunchLogic()),
        };

        public static Mode Of(Category category) => All[(int)category];

        public static ModePage[] CreatePages(Func<bool> superChosen) => new ModePage[]
        {
            new RoundsPage(),
            new BuildablesPage(),
            new RainbowPage(),
            new SongsPage(),
            new EasterEggPage(superChosen),
        };
    }
}
