using AssetSystem;
using CommonUtilities.Pool;
using System;
using System.Collections.Generic;
using WorldEditor;

namespace MapScanner
{
    public class SectionCollection : ISectionCollection
    {
        public int HighestSectionY { get; private set; } = int.MinValue;
        public int LowestSectionY { get; private set; } = int.MaxValue;

        public IResettablePool<short[]> Pool { get; set; }

        private IAsset<Block, BlockGrouping> _asset;

        /// <summary>
        /// Sections indexed by <c>y - LowestSectionY</c> rather than keyed by Y.
        /// <see cref="Provide"/> runs once per section per column - roughly 6000 times per
        /// chunk - so this is an array index instead of a hash lookup, and one allocation
        /// per chunk instead of a dictionary plus a holder object per section.
        /// </summary>
        private Slot[] _slots = Array.Empty<Slot>();

        /// <summary>
        /// Providers handed out this chunk, reused across chunks. The instances (and their
        /// grouping tables) survive <see cref="Reinitialize"/>; only their content is reset.
        /// </summary>
        private readonly List<SectionedBlockProvider> _providers = new();
        private int _providersUsed;

        private struct Slot
        {
            public SectionGroup Group;
            public SectionedBlockProvider? Provider;
            public bool Present;
        }

        public SectionCollection(IAsset<Block, BlockGrouping> asset, ConvertedApiChunk apiChunk, IResettablePool<short[]> pool)
        {
            Pool = pool;
            _asset = asset;

            Reinitialize(asset, apiChunk);
        }

        /// <summary>
        /// Points this collection at a new chunk, reusing the slot and provider storage. The
        /// asset is taken again because a style change swaps it on the factory mid-session.
        /// </summary>
        public void Reinitialize(IAsset<Block, BlockGrouping> asset, ConvertedApiChunk apiChunk)
        {
            _asset = asset;
            HighestSectionY = int.MinValue;
            LowestSectionY = int.MaxValue;
            _providersUsed = 0;

            BlockStateChunk? blockStateChunk = apiChunk.BlockState;
            if (blockStateChunk is null || blockStateChunk.Sections.Count == 0)
            {
                _slots.AsSpan().Clear();
                return;
            }

            AddSections(apiChunk, blockStateChunk);
        }

        private void AddSections(ConvertedApiChunk apiChunk, BlockStateChunk blockStateChunk)
        {
            IList<PaletteSection<Block>> blockSections = blockStateChunk.Sections;

            for (int i = 0; i < blockSections.Count; i++)
            {
                sbyte y = blockSections[i].Y;
                if (y > HighestSectionY) HighestSectionY = y;
                if (y < LowestSectionY) LowestSectionY = y;
            }

            int span = HighestSectionY - LowestSectionY + 1;
            if (_slots.Length < span)
            {
                _slots = new Slot[span];
            }
            else
            {
                _slots.AsSpan().Clear();
            }

            Span<short> biomeAt = stackalloc short[SectionIndex.Size];
            Span<short> skyLightAt = stackalloc short[SectionIndex.Size];
            Span<short> blockLightAt = stackalloc short[SectionIndex.Size];

            IList<PaletteSection<string>>? biomes = apiChunk.Biome?.Sections;
            IList<LightChunk.Section>? skyLights = apiChunk.SkyLight?.Sections;
            IList<LightChunk.Section>? blockLights = apiChunk.BlockLight?.Sections;

            SectionIndex.Build(biomes, biomeAt);
            SectionIndex.Build(skyLights, skyLightAt);
            SectionIndex.Build(blockLights, blockLightAt);

            for (int i = 0; i < blockSections.Count; i++)
            {
                PaletteSection<Block> blockSection = blockSections[i];
                int y = blockSection.Y;

                _slots[y - LowestSectionY] = new Slot
                {
                    Present = true,
                    Group = new SectionGroup(blockSection)
                    {
                        BiomeSection = SectionIndex.Get(biomes, biomeAt, y),
                        SkyLightSection = SectionIndex.Get(skyLights, skyLightAt, y),
                        BlockLightSection = SectionIndex.Get(blockLights, blockLightAt, y),
                        AboveSkyLightSection = SectionIndex.Get(skyLights, skyLightAt, y + 1),
                        AboveBlockLightSection = SectionIndex.Get(blockLights, blockLightAt, y + 1)
                    }
                };
            }
        }

        public ISectionedBlockProvider? Provide(int y)
        {
            int index = y - LowestSectionY;
            if ((uint)index >= (uint)_slots.Length) return null;

            ref Slot slot = ref _slots[index];
            if (!slot.Present) return null;

            return slot.Provider ??= RentProvider(slot.Group);
        }

        private SectionedBlockProvider RentProvider(SectionGroup group)
        {
            if (_providersUsed < _providers.Count)
            {
                SectionedBlockProvider reused = _providers[_providersUsed++];
                reused.Initialize(_asset, group, Pool);
                return reused;
            }

            SectionedBlockProvider created = new(_asset, group, Pool);
            _providers.Add(created);
            _providersUsed++;

            return created;
        }

        public void Dispose()
        {
            Pool.Reset();
        }
    }
}
