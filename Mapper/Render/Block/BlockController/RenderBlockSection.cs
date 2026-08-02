using AssetSystem.Biome;
using MapScanner;
using WorldEditor;

namespace Mapper
{
    public class RenderBlockSection : IDisposable
    {
        public AssetPack AssetPack { get; set; }
        public Section<Block> BlockSection { get; set; }
        public Section<string>? BiomeSection { get; set; }

        public string DefaultBiome { get; set; } = "minecraft:plains";

        // Rented: one section table set is built for every section of every rendered chunk,
        // which allocated several GB per large load. Rented arrays may be longer than
        // requested and carry stale entries past the filled range; every read is bounded by
        // the palette sizes.
        //
        // Entries resolve lazily on first use. A section's tables used to be filled eagerly
        // for every palette x biome combination - several thousand asset lookups per chunk -
        // while the chunk's columns only ever query the handful of combinations that actually
        // occur. A null render block marks an unresolved combination.
        private RenderBlock?[] _renderBlocks;
        private StepSettings[] _stepSettings;
        private DepthOpacity[] _depthOpacity;
        private RgbA[] _blockColors;
        private bool[] _blockResolved;

        private readonly int _biomeLength;

        public RenderBlockSection(AssetPack assetPack, Section<Block> blockSection, Section<string>? biomeSection)
        {
            AssetPack = assetPack;
            BlockSection = blockSection;
            BiomeSection = biomeSection;

            _biomeLength = BiomeSection?.Palette?.Length ?? 1;
            int paletteLength = BlockSection.Palette.Length;
            int combinations = paletteLength * _biomeLength;

            _renderBlocks = RenderArrayPool<RenderBlock?>.Rent(combinations);
            _stepSettings = RenderArrayPool<StepSettings>.Rent(paletteLength);
            _depthOpacity = RenderArrayPool<DepthOpacity>.Rent(combinations);
            _blockColors = RenderArrayPool<RgbA>.Rent(paletteLength);
            _blockResolved = RenderArrayPool<bool>.Rent(paletteLength);

            // Only the sentinels need clearing; the value tables are valid wherever their
            // sentinel says so.
            Array.Clear(_renderBlocks, 0, combinations);
            Array.Clear(_blockResolved, 0, paletteLength);
        }

        private void ResolveBlock(int blockIndex)
        {
            _blockColors[blockIndex] = AssetPack.BlockColorAsset.Provide(BlockSection.Palette[blockIndex]);
            _stepSettings[blockIndex] = AssetPack.StepSettingsAsset.Provide(BlockSection.Palette[blockIndex]);
            _blockResolved[blockIndex] = true;
        }
        private RenderBlock ResolveCombination(int index, int blockIndex, int biomeIndex)
        {
            if (!_blockResolved[blockIndex]) ResolveBlock(blockIndex);

            BiomeBlock parameter = new(BlockSection.Palette[blockIndex], BiomeSection?.Palette?[biomeIndex] ?? DefaultBiome);

            VecRgb biomeColor = AssetPack.BiomeColorAsset.Provide(parameter);
            ElevationSettings elevationSettings = AssetPack.ElevationAsset.Provide(parameter);
            _depthOpacity[index] = AssetPack.DepthOpacityAsset.Provide(parameter);

            RenderBlock renderBlock = new RenderBlock(_blockColors[blockIndex], biomeColor, elevationSettings);
            _renderBlocks[index] = renderBlock;

            return renderBlock;
        }

        public RenderBlock ProvideRenderBlock(BlockData blockData)
        {
            int index = blockData.IndexInBlockPalette * _biomeLength + blockData.IndexInBiomePalette;
            return _renderBlocks[index] ?? ResolveCombination(index, blockData.IndexInBlockPalette, blockData.IndexInBiomePalette);
        }
        public DepthOpacity ProvideDepthOpacity(BlockData blockData)
        {
            int index = blockData.IndexInBlockPalette * _biomeLength + blockData.IndexInBiomePalette;
            if (_renderBlocks[index] is null) ResolveCombination(index, blockData.IndexInBlockPalette, blockData.IndexInBiomePalette);

            return _depthOpacity[index];
        }
        public StepSettings ProvideStepSettings(BlockData blockData)
        {
            if (!_blockResolved[blockData.IndexInBlockPalette]) ResolveBlock(blockData.IndexInBlockPalette);

            return _stepSettings[blockData.IndexInBlockPalette];
        }

        private bool _disposed;

        public void Dispose()
        {
            // Guarded because returning the same array to the shared pool twice corrupts it.
            if (_disposed) return;
            _disposed = true;

            RenderArrayPool<RenderBlock?>.Return(_renderBlocks);
            RenderArrayPool<StepSettings>.Return(_stepSettings);
            RenderArrayPool<DepthOpacity>.Return(_depthOpacity);
            RenderArrayPool<RgbA>.Return(_blockColors);
            RenderArrayPool<bool>.Return(_blockResolved);
        }
    }
}
