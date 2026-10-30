using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.T6ZM
{
    internal readonly struct Buildable
    {
        public Buildable(string key, string label, bool optional = false)
        {
            Key = key;
            Label = label;
            Optional = optional;
        }

        public string Key { get; }
        public string Label { get; }
        public bool Optional { get; }
    }

    internal sealed class BuildableMap
    {
        public BuildableMap(string script, string name, params Buildable[] items)
        {
            Script = script;
            Name = name;
            Items = items;
        }

        public string Script { get; }
        public string Name { get; }
        public IReadOnlyList<Buildable> Items { get; }
        public string Note { get; set; }
        public uint Every => (uint)((1UL << Items.Count) - 1);
        public uint Required
        {
            get
            {
                uint mask = 0;
                for (int i = 0; i < Items.Count; i++)
                    if (!Items[i].Optional) mask |= 1u << i;
                return mask;
            }
        }
    }

    internal static class BuildableMaps
    {
        public static readonly BuildableMap[] All =
        {
            new BuildableMap("zm_transit", "TranZit",
                new Buildable("turbine", "Turbine"), new Buildable("riotshield_zm", "Zombie Shield"), new Buildable("turret", "Turret"),
                new Buildable("electric_trap", "Electric Trap"), new Buildable("powerswitch", "Power Switch"),
                new Buildable("jetgun_zm", "Jetgun"), new Buildable("pap", "Pack-a-Punch"),
                new Buildable("cattlecatcher", "Cow Catcher"), new Buildable("bushatch|dinerhatch", "Hatch"),
                new Buildable("busladder", "Ladder"), new Buildable("sq_common", "NAV Table")),
            new BuildableMap("zm_highrise", "Die Rise",
                new Buildable("springpad_zm", "Trample Steam"), new Buildable("slipgun_zm", "Sliquifier"), new Buildable("sq_common", "NAV Table")),
            new BuildableMap("zm_buried", "Buried",
                new Buildable("turbine", "Turbine"), new Buildable("headchopper_zm", "Head Chopper"), new Buildable("subwoofer_zm", "Subsurface Resonator"),
                new Buildable("sq_common", "NAV Table"), new Buildable("springpad_zm", "Trample Steam"),
                new Buildable("buried_sq_bt_m_tower|buried_sq_bt_r_tower", "Guillotine / Gallows", optional: true),
                new Buildable("buried_sq_oillamp", "Oil Lamp", optional: true), new Buildable("buried_sq_tpo_switch", "Switch", optional: true)),
            new BuildableMap("zm_prison", "Mob of the Dead",
                new Buildable("alcatraz_shield_zm", "Zombie Shield"), new Buildable("plane", "Plane"), new Buildable("packasplat", "Acid Gat Kit")),
            new BuildableMap("zm_tomb", "Origins",
                new Buildable("equip_dieseldrone_zm", "Maxis Drone"), new Buildable("tomb_shield_zm", "Zombie Shield"),
                new Buildable("elemental_staff_fire", "Fire Staff"), new Buildable("elemental_staff_air", "Wind Staff"),
                new Buildable("elemental_staff_lightning", "Lightning Staff"), new Buildable("elemental_staff_water", "Ice Staff")),
        };

        public static BuildableMap For(string script) => All.FirstOrDefault(m => m.Script == script);
    }

    internal sealed class BuildablesLogic : RunLogic
    {
        const int MaskWatch = 1;

        readonly Func<string, uint> chosen;
        string map = "";
        uint built;
        uint settled;

        public BuildablesLogic(Func<string, uint> chosen) => this.chosen = chosen;

        public override ReaderProfile Profile => ReaderProfile.Buildables;

        public void SetMap(string script)
        {
            if (script != map && !string.IsNullOrEmpty(script)) built = settled = 0;
            if (!string.IsNullOrEmpty(script)) map = script;
        }

        public override bool SupportsMap(string script) => BuildableMaps.For(script) != null;

        protected override void Reset() => settled = built;

        protected override void OnNewLevel() => built = settled = 0;

        public override IEnumerable<TimerAction> OnChange(GameEvent e, bool running)
        {
            BuildableMap current = BuildableMaps.For(map);
            uint before = built;
            if (e.Watch == MaskWatch) built |= (uint)(e.OldValue | e.NewValue);
            if (!running)
            {
                if (BlackscreenEnded(e)) yield return StartRun(e.Clock);
                yield break;
            }
            if (e.Watch != MaskWatch || current == null) yield break;

            uint required = current.Required;
            uint fresh = (uint)e.NewValue & ~(uint)e.OldValue & ~settled & current.Every;
            if (fresh == 0) yield break;
            uint pick = chosen(map);
            int finalBit = (before & required) != required && (built & required) == required ? 31 - LeadingZeros(fresh & required) : -1;
            for (int bit = 0; bit < current.Items.Count; bit++)
            {
                uint mask = 1u << bit;
                if ((fresh & mask) == 0) continue;
                settled |= mask;
                if ((pick & mask) != 0 || bit == finalBit)
                    yield return new TimerAction(false, e.Clock, current.Items[bit].Label);
            }
        }

        public override string Next
        {
            get
            {
                BuildableMap current = BuildableMaps.For(map);
                if (current == null) return "map not supported";
                int total = Count(current.Required);
                int left = total - Count(built & current.Required);
                return left == 0 ? "done" : $"{left} of {total} buildables to go";
            }
        }

        static int LeadingZeros(uint mask)
        {
            int n = 0;
            for (uint bit = 0x80000000; (mask & bit) == 0 && n < 32; bit >>= 1) n++;
            return n;
        }

        static int Count(uint mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }
    }
}
