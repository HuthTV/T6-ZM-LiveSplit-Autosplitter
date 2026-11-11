using System.Xml;

namespace LiveSplit.T6ZM
{
    internal sealed class RainbowPage : StepListPage<StepList<RainbowStep>, RainbowStep>
    {
        public RainbowPage() : base(RainbowMaps.All, "Map:", mapWidth: 220, rows: 8, hasNote: false, honourForced: false) { }

        public RunLogic CreateLogic() => new RainbowLogic(Mask);

        public uint Mask(string script) => Mask(RainbowMaps.For(script));

        public override void WriteXml(XmlDocument document, XmlElement node) => WriteTicks(document, node, "Rainbow_", ',');

        public override void ReadXml(XmlNode node) =>
            ReadTicks(node, "Rainbow_", ',', ids => ids.Replace("Red dig ", "Bottle "));
    }
}
