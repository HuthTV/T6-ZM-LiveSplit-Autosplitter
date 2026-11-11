namespace LiveSplit.T6ZM
{
    internal static class GameMaps
    {
        public static string Name(string script)
        {
            switch (script)
            {
                case "zm_transit": return "TranZit";
                case "zm_nuked": return "Nuketown";
                case "zm_highrise": return "Die Rise";
                case "zm_prison": return "Mob of the Dead";
                case "zm_buried": return "Buried";
                case "zm_tomb": return "Origins";
                default: return string.IsNullOrEmpty(script) ? "map ?" : script;
            }
        }
    }
}
