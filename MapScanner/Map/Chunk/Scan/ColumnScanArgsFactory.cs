using AssetSystem;
using CommonUtilities.Factory;
using CommonUtilities.Pool;
using WorldEditor;

namespace MapScanner
{
    public class ColumnScanArgsFactory : IFactory<ColumnScanArgsFactoryArgs, ColumnScanArgs>
    {
        public IAsset<Block, BlockGrouping> Asset { get; set; }

        public bool UseHeightmap { get; set; } = true;
        public string Heightmap { get; set; } = "WORLD_SURFACE";
        public short SetY { get; set; } = 319;

        public IPool<IResettablePool<short[]>> Pools { get; set; }

        /// <summary>
        /// One reusable scan graph per worker slot: section collection, level providers,
        /// builder and output objects. Everything a chunk's scan needs besides its results
        /// used to be rebuilt per chunk - a dictionary, a slot table and half a dozen
        /// objects each time - and is now reset in place.
        /// </summary>
        protected sealed class SlotGraph
        {
            public SectionCollection? Sections;
            public HeightmapLevelProvider? HeightmapProvider;
            public SimpleLevelProvider? SimpleProvider;
            public ColumnObjectBuilder? Builder;
            public StandardBlockOutput? StandardOutput;
            public CaveBlockOutput? CaveOutput;
            public ColumnScanArgs? Args;
        }

        private readonly IPool<SlotGraph> _graphs;

        /// <param name="slotCount">
        /// Number of concurrent scan slots to pre-size the per-slot pools for. Must cover the
        /// caller's <c>regions * chunks</c> concurrency; the pool grows if it is too small, but
        /// growing costs allocations on the first regions of a load.
        /// </param>
        public ColumnScanArgsFactory(IAsset<Block, BlockGrouping> asset, int slotCount = 32)
        {
            Asset = asset;
            ExpandableIndexedPool<IResettablePool<short[]>> indexedPool = new ExpandableIndexedPool<IResettablePool<short[]>>(slotCount, i => new ExpandableObjectPool<short[]>(10, () => new short[4096]));
            Pools = new ThreadedPool<IResettablePool<short[]>>(indexedPool);

            _graphs = new ThreadedPool<SlotGraph>(new ExpandableIndexedPool<SlotGraph>(slotCount, i => new SlotGraph()));
        }

        public virtual ColumnScanArgs Create(ColumnScanArgsFactoryArgs args)
        {
            SlotGraph graph = _graphs.Provide();

            ISectionCollection sectionCollection = CreateSectionCollection(args, graph);
            ILevelProvider levelProvider = CreateLevelProvider(args, graph);
            IBlockOutput blockOutput = CreateBlockOutput(args, graph);

            ColumnScanArgs? scanArgs = graph.Args;
            if (scanArgs is null)
            {
                graph.Args = scanArgs = new ColumnScanArgs(sectionCollection, levelProvider, blockOutput);
            }
            else
            {
                scanArgs.SectionCollection = sectionCollection;
                scanArgs.LevelProvider = levelProvider;
                scanArgs.BlockOutput = blockOutput;
            }

            return scanArgs;
        }

        protected virtual ISectionCollection CreateSectionCollection(ColumnScanArgsFactoryArgs args, SlotGraph graph)
        {
            if (graph.Sections is null)
            {
                graph.Sections = new SectionCollection(Asset, args.ApiChunk, Pools.Provide());
            }
            else
            {
                graph.Sections.Pool = Pools.Provide();
                graph.Sections.Reinitialize(Asset, args.ApiChunk);
            }

            return graph.Sections;
        }
        protected virtual ILevelProvider CreateLevelProvider(ColumnScanArgsFactoryArgs args, SlotGraph graph)
        {
            if (UseHeightmap && args.ApiChunk.Heightmap is not null && args.ApiChunk.Heightmap is HeightmapCollection collection &&
                collection.Heightmaps.TryGetValue(Heightmap, out HeightmapCollection.Heightmap? heightmap))
            {
                if (graph.HeightmapProvider is null)
                {
                    graph.HeightmapProvider = new HeightmapLevelProvider(heightmap);
                }
                else
                {
                    graph.HeightmapProvider.Reset(heightmap);
                }

                return graph.HeightmapProvider;
            }
            else
            {
                graph.SimpleProvider ??= new SimpleLevelProvider(SetY);
                graph.SimpleProvider.Y = SetY;

                return graph.SimpleProvider;
            }
        }
        protected virtual IBlockOutput CreateBlockOutput(ColumnScanArgsFactoryArgs args, SlotGraph graph)
        {
            ColumnObjectBuilder builder = ProvideBuilder(args, graph);

            graph.StandardOutput ??= new StandardBlockOutput(builder);
            return graph.StandardOutput;
        }

        protected static ColumnObjectBuilder ProvideBuilder(ColumnScanArgsFactoryArgs args, SlotGraph graph)
        {
            if (graph.Builder is null)
            {
                graph.Builder = new ColumnObjectBuilder(args.ScannedChunk.UniqueColumns, args.ScannedChunk.Indexes);
            }
            else
            {
                graph.Builder.Reset(args.ScannedChunk.UniqueColumns, args.ScannedChunk.Indexes);
            }

            return graph.Builder;
        }
    }
}
