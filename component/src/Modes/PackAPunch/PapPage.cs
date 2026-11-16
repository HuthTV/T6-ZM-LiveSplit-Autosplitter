using System.Xml;

namespace LiveSplit.T6ZM
{
    internal sealed class PapPage : StepListPage<PapMap, PapStep>
    {
        public PapPage() : base(PapMaps.All, "Map:", mapWidth: 220, rows: 9, hasNote: false, honourForced: true) { }

        public RunLogic CreateLogic() => new PackAPunchLogic(Mask);

        public uint Mask(string key) => Mask(PapMaps.ByKey(key));

        public override void WriteXml(XmlDocument document, XmlElement node) => WriteTicks(document, node, "Pap_", ',');

        public override void ReadXml(XmlNode node) => ReadTicks(node, "Pap_", ',');
    }
}
