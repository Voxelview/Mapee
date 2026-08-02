using AssetSystem;
using CommonUtilities.Pool;
using System;
using System.Threading;
using WorldEditor;

namespace MapScanner
{
    public class SectionedBlockProvider : ISectionedBlockProvider
    {
        public IAsset<Block, BlockGrouping> Asset { get; set; }

        public SectionGroup SectionGroup { get; set; }

        private BlockGrouping[] _blockGroupings = Array.Empty<BlockGrouping>();
        private int _paletteLength;
        private short[] _blockIndexes = Array.Empty<short>();
        private short[] _biomeIndexes = Array.Empty<short>();
        private IPool<short[]> _pool;

        private bool _isBlockUniform = false, _isBiomeUniform = false;

        public SectionedBlockProvider(IAsset<Block, BlockGrouping> asset, SectionGroup sectionGroup, IPool<short[]> pool)
        {
            Asset = asset;
            SectionGroup = sectionGroup;
            _pool = pool;

            Initialize(asset, sectionGroup, pool);
        }

        /// <summary>
        /// Re-points a pooled instance at a new section. The grouping table grows to the
        /// largest palette the instance has seen and is refilled up to the current palette's
        /// length on each call.
        /// </summary>
        public void Initialize(IAsset<Block, BlockGrouping> asset, SectionGroup sectionGroup, IPool<short[]> pool)
        {
            Asset = asset;
            SectionGroup = sectionGroup;
            _pool = pool;

            Block[] palette = SectionGroup.BlockSection.Palette;
            _paletteLength = palette.Length;
            if (_blockGroupings.Length < _paletteLength)
            {
                _blockGroupings = new BlockGrouping[_paletteLength];
            }

            if (SectionGroup.BlockSection.Type == SectionType.Normal)
            {
                _blockIndexes = _pool.Provide();
                SectionGroup.BlockSection.Unlock(_blockIndexes);
                _isBlockUniform = false;
            }
            else
            {
                _isBlockUniform = true;
            }

            for (int i = 0; i < _paletteLength; i++)
            {
                _blockGroupings[i] = Asset.Provide(palette[i]);
            }

            if (SectionGroup.BiomeSection?.Type == SectionType.Normal)
            {
                if (SectionGroup.BiomeSection.Locker is null) return;

                // Same pool the block indexes come from. This used to allocate a fresh 8 KB
                // array for every biome section of every chunk. Provide() hands out a distinct
                // array each call, and the pool is reset once the chunk is done.
                _biomeIndexes = _pool.Provide();
                SectionGroup.BiomeSection.Unlock(_biomeIndexes);
                _isBiomeUniform = false;
            }
            else
            {
                _isBiomeUniform = true;
            }
        }

        public BlockGrouping ProvideGrouping(int index)
        {
            int blockIndex = GetBlockIndex(index);

            // A reused table can be longer than the current palette; malformed data whose
            // packed index escapes the palette reads as empty instead of stale state.
            if ((uint)blockIndex >= (uint)_paletteLength) return BlockGrouping.Empty;
            return _blockGroupings[blockIndex];
        }
        public Block ProvideBlock(int index)
        {
            return SectionGroup.BlockSection.Palette[GetBlockIndex(index)];
        }
        public short ProvideIndexInPalette(int index)
        {
            return (short)GetBlockIndex(index);
        }
        public BlockData ProvideBlockData(int index, bool isTopBlock = false)
        {
            ushort indexInBlockPalette = (ushort)ProvideIndexInPalette(index);
            ushort indexInBiomePalette = (ushort)GetBiomeIndex(MathUtilities.FindBiomeIndex(index % 16, index / 256, (index % 256) / 16));

            byte skyLight = GetSkyLight(index + 256, isTopBlock);
            byte blockLight = GetBlockLight(index + 256);

            return new BlockData(indexInBlockPalette, indexInBiomePalette, skyLight, blockLight);
        }

        private int GetBlockIndex(int blockIndex)
        {
            if (_isBlockUniform) return 0;
            return _blockIndexes[blockIndex];
        }

        private int GetBiomeIndex(int biomeIndex)
        {
            if (_isBiomeUniform) return 0;
            return _biomeIndexes[biomeIndex];
        }

        private byte GetSkyLight(int index, bool isTopBlock)
        {
            if (isTopBlock) return 15;

            bool above = index > 4095;
            index %= 4096;

            LightChunk.Section? section;
            if (above)
            {
                section = SectionGroup.AboveSkyLightSection;
            }
            else
            {
                section = SectionGroup.SkyLightSection;
            }

            return section?.GetLight(index) ?? 0;
        }
        private byte GetBlockLight(int index)
        {
            bool above = index > 4095;
            index %= 4096;

            LightChunk.Section? section;
            if (above)
            {
                section = SectionGroup.AboveBlockLightSection;
            }
            else
            {
                section = SectionGroup.BlockLightSection;
            }

            return section?.GetLight(index) ?? 0;
        }

        public void Dispose()
        {
            Array.Clear(_blockGroupings);
            _blockIndexes = Array.Empty<short>();
            _biomeIndexes = Array.Empty<short>();
        }
    }
}
