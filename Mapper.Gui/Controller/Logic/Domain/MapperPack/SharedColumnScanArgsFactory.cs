using AssetSystem;
using MapScanner;
using WorldEditor;

namespace Mapper.Gui.Logic
{
    public class SharedColumnScanArgsFactory : ColumnScanArgsFactory
    {
        public ScanType ScanType { get; set; }

        public SharedColumnScanArgsFactory(IAsset<Block, BlockGrouping> asset, int slotCount)
            : base(asset, slotCount) { }

        protected override IBlockOutput CreateBlockOutput(ColumnScanArgsFactoryArgs args, SlotGraph graph)
        {
            ColumnObjectBuilder columnObjectBuilder = ProvideBuilder(args, graph);

            switch (ScanType)
            {
                case ScanType.Cave:
                    graph.CaveOutput ??= new CaveBlockOutput(columnObjectBuilder);
                    graph.CaveOutput.Builder = columnObjectBuilder;
                    return graph.CaveOutput;
                default:
                    return graph.StandardOutput ??= new StandardBlockOutput(columnObjectBuilder);
            }
        }
    }
}
