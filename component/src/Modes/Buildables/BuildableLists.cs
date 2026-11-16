using System.Linq;

namespace LiveSplit.T6ZM
{
    internal sealed class Buildable : Step
    {
        public Buildable(string key, string label, bool optional = false) : base(key, label) => Optional = optional;

        public bool Optional { get; }
    }

    internal sealed class BuildableMap : StepList<Buildable>
    {
        public BuildableMap(string script, params Buildable[] items) : base(script, script, items) { }

        public uint Required
        {
            get
            {
                uint mask = 0;
                for (int i = 0; i < Steps.Count; i++)
                    if (!Steps[i].Optional) mask |= 1u << i;
                return mask;
            }
        }

        public override uint Default => Required;
    }

    internal static class BuildableMaps
    {
        public static readonly BuildableMap[] All =
        {
            new BuildableMap("zm_transit",
                new Buildable("turbine", "Turbine"), new Buildable("riotshield_zm", "Zombie Shield"), new Buildable("turret", "Turret"),
                new Buildable("electric_trap", "Electric Trap"), new Buildable("powerswitch", "Power Switch"),
                new Buildable("jetgun_zm", "Jetgun"), new Buildable("pap", "Pack-a-Punch"),
                new Buildable("cattlecatcher", "Cow Catcher"), new Buildable("bushatch|dinerhatch", "Hatch"),
                new Buildable("busladder", "Ladder"), new Buildable("sq_common", "NAV Table", optional: true)),
            new BuildableMap("zm_highrise",
                new Buildable("springpad_zm", "Trample Steam"), new Buildable("slipgun_zm", "Sliquifier"), new Buildable("sq_common", "NAV Table")),
            new BuildableMap("zm_buried",
                new Buildable("turbine", "Turbine"), new Buildable("headchopper_zm", "Head Chopper"), new Buildable("subwoofer_zm", "Subsurface Resonator"),
                new Buildable("sq_common", "NAV Table"), new Buildable("springpad_zm", "Trample Steam"),
                new Buildable("buried_sq_bt_m_tower|buried_sq_bt_r_tower", "Guillotine / Gallows", optional: true),
                new Buildable("buried_sq_oillamp", "Oil Lamp", optional: true), new Buildable("buried_sq_tpo_switch", "Switch", optional: true)),
            new BuildableMap("zm_prison",
                new Buildable("alcatraz_shield_zm", "Zombie Shield"), new Buildable("plane", "Plane"), new Buildable("packasplat", "Acid Gat Kit")),
            new BuildableMap("zm_tomb",
                new Buildable("equip_dieseldrone_zm", "Maxis Drone"), new Buildable("tomb_shield_zm", "Zombie Shield"),
                new Buildable("elemental_staff_fire", "Fire Staff"), new Buildable("elemental_staff_air", "Wind Staff"),
                new Buildable("elemental_staff_lightning", "Lightning Staff"), new Buildable("elemental_staff_water", "Ice Staff")),
        };

        public static BuildableMap For(string script) => All.FirstOrDefault(m => m.Script == script);
    }
}
