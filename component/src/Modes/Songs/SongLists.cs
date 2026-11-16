using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class SongStep : Step
    {
        public const int Event = -1;

        public SongStep(string label, int watch, int level, bool extra = false, string items = null, string id = null) : base(id ?? label, label)
        {
            Watch = watch;
            Level = level;
            Extra = extra;
            Items = items;
        }

        public int Watch { get; }
        public int Level { get; }
        public bool Extra { get; }
        public string Items { get; }
    }

    internal sealed class SongMap : StepList<SongStep>
    {
        public SongMap(string key, string script, string song, params SongStep[] steps)
            : base(key, script, steps, $"({GameMaps.Name(script)}) {song}", forced: 1u << (steps.Length - 1)) { }

        public SongMap(string key, string script, string[] parts)
            : this(key, script, SongMaps.AllSongs, new SongStep(SongMaps.AllSongs, SongStep.Event, 0)) => Parts = parts;

        public string[] Parts { get; }

        public override uint Default
        {
            get
            {
                uint mask = 0;
                for (int i = 0; i < Steps.Count; i++)
                    if (!Steps[i].Extra) mask |= 1u << i;
                return mask | Forced;
            }
        }
    }

    internal static class SongMaps
    {
        static SongStep[] Counted(string what, string items, int watch, string savedAs = null) =>
            Enumerable.Range(1, 3).Select(n => new SongStep($"{what} {n}", watch, n, items: items, id: savedAs == null ? null : $"{savedAs} {n}")).ToArray();

        static SongMap Song(string key, string script, string song, params SongStep[][] groups) =>
            new SongMap(key, script, song, groups.SelectMany(g => g).ToArray());

        static SongStep[] Start(string song) => new[] { new SongStep(song, SongStep.Event, 0) };

        static SongStep[] Rounds(int last) =>
            Enumerable.Range(1, last).Select(k => new SongStep($"End of round {k}", SongsLogic.RoundWatch, k + 1, true)).ToArray();

        static SongStep[] Extra(string label, int watch) => new[] { new SongStep(label, watch, 1, true) };

        public const string AllSongs = "All songs";

        public static readonly SongMap[] All =
        {
            Song("transit_carrion", "zm_transit", "Carrion", Rounds(6), Counted("Teddy", "teddies", SongsLogic.MeteorWatch)),
            Song("nuked_samantha", "zm_nuked", "Samantha's Lullaby", Rounds(6), Counted("Teddy", "teddies", SongsLogic.MeteorWatch)),
            Song("nuked_redamned", "zm_nuked", "Re-Damned", Rounds(6), Start("Re-Damned")),
            Song("nuked_cominghome", "zm_nuked", "Coming Home", Rounds(6), Start("Coming Home")),
            new SongMap("nuked_all", "zm_nuked", new[] { "nuked_samantha", "nuked_redamned", "nuked_cominghome" }),
            Song("highrise_fall", "zm_highrise", "We All Fall Down", Rounds(6), Counted("Teddy", "teddies", SongsLogic.MeteorWatch)),
            Song("prison_rustycage", "zm_prison", "Rusty Cage", Rounds(6), Counted("Bottle", "bottles", SongsLogic.MeteorWatch)),
            Song("prison_whereare", "zm_prison", "Where Are We Going", Rounds(6), Start("Afterlife revive"), Start("Where Are We Going")),
            new SongMap("prison_all", "zm_prison", new[] { "prison_rustycage", "prison_whereare" }),
            Song("buried_running", "zm_buried", "Always Running", Rounds(6), Counted("Teddy", "teddies", SongsLogic.MeteorWatch)),
            Song("tomb_shepherd", "zm_tomb", "Shepherd of Fire", Rounds(6), Counted("Radio", "radios", SongsLogic.RadioWatch)),
            Song("tomb_archangel", "zm_tomb", "Archangel", Rounds(6), Counted("Meteorite", "meteorites", SongsLogic.MeteorWatch, "Green stone")),
            Song("tomb_aether", "zm_tomb", "Aether", Extra("No Man's Land", SongsLogic.NmlWatch), Rounds(6),
                new[] { new SongStep("Aether", SongsLogic.AetherWatch, 3, items: "plates") }),
            new SongMap("tomb_all", "zm_tomb", new[] { "tomb_shepherd", "tomb_archangel", "tomb_aether" }),
        };

        public static SongMap ByKey(string key) => All.FirstOrDefault(m => m.Key == key);
    }
}
