using System.Xml;

namespace LiveSplit.T6ZM
{
    internal sealed class BuildablesPage : StepListPage<BuildableMap, Buildable>
    {
        public BuildablesPage() : base(BuildableMaps.All, "Map:", mapWidth: 220, rows: 11, honourForced: false) { }

        public RunLogic CreateLogic() => new BuildablesLogic(Mask);

        public uint Mask(string script) => Mask(BuildableMaps.For(script));

        public override void WriteXml(XmlDocument document, XmlElement node) => WriteTicks(document, node, "Built_", ',');

        public override void ReadXml(XmlNode node) => ReadTicks(node, "Built_", ',');
    }
}
