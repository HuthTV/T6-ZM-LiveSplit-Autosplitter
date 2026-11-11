using System;
using System.Xml;
using static LiveSplit.T6ZM.PageParts;

namespace LiveSplit.T6ZM
{
    internal sealed class SongsPage : StepListPage<SongMap, SongStep>
    {
        public SongsPage() : base(SongMaps.All, "Song:", mapWidth: 300, rows: 9, hasNote: true, honourForced: true) { }

        public RunLogic CreateLogic() => new SongsLogic(Selected, Mask);

        public uint Mask(string key) => Mask(SongMaps.ByKey(key));

        public override void UpdateEnabled(bool locked, bool timingsHeld) => MapBox.Enabled = !locked;

        public override void SelectGameMap(string game, bool locked)
        {
            int index = Array.FindIndex(SongMaps.All, m => m.Script == game);
            if (index >= 0 && !locked && Selected.Script != game) MapBox.SelectedIndex = index;
        }

        public override void WriteXml(XmlDocument document, XmlElement node)
        {
            node.AppendChild(Element(document, "Song", Selected.Key));
            WriteTicks(document, node, "Songs_", '|');
        }

        public override void ReadXml(XmlNode node)
        {
            int song = Array.FindIndex(SongMaps.All, m => m.Key == node?["Song"]?.InnerText);
            if (song >= 0) MapBox.SelectedIndex = song;
            ReadTicks(node, "Songs_", '|');
        }
    }
}
