using CommonUtilities.Factory;
using System;
using System.Collections.Generic;
using WorldEditor;

namespace MapScanner
{
    public class ChunkScanner : IObjectScanner<ConvertedApiChunk, ScannedChunk>
    {
        public IObjectScanner<ColumnScanArgs> ColumnScanner { get; set; }
        public IFactory<ColumnScanArgsFactoryArgs, ColumnScanArgs> ColumnScanParameterFactory { get; set; }

        public ChunkScanner(IObjectScanner<ColumnScanArgs> columnScanner, IFactory<ColumnScanArgsFactoryArgs, ColumnScanArgs> columnScanParameterFactory)
        {
            ColumnScanner = columnScanner;
            ColumnScanParameterFactory = columnScanParameterFactory;
        }

        public ScannedChunk Scan(ConvertedApiChunk chunk)
        {
            ScannedChunk output = new(new Coords(chunk.X, chunk.Z));
            ColumnScanArgs scanParameter = ColumnScanParameterFactory.Create(new ColumnScanArgsFactoryArgs() 
            {
                ApiChunk = chunk,
                ScannedChunk = output
            });

            int maxY = int.MinValue, minY = int.MaxValue;
            for (int i = 0; i < 256; i++)
            {
                scanParameter.BlockOutput.BeginScan();
                ColumnScanner.Scan(scanParameter.Copy(i % 16, i / 16));
                scanParameter.BlockOutput.EndScan();

                ScannedColumn column = output.GetColumn(i);
                if (column.Type == ColumnType.Empty) continue;

                GetColumnRange(column, out int colMaxY, out int colMinY);
                if (colMaxY > maxY) maxY = colMaxY;
                if (colMinY < minY) minY = colMinY;
            }

            SetSections(maxY, minY, chunk, output);
            scanParameter.SectionCollection.Dispose();

            return output;
        }

        private static void GetColumnRange(ScannedColumn column, out int maxY, out int minY)
        {
            if (column.BlockSpans.Length > 0)
            {
                ReadOnlySpan<BlockSpan> span = column.BlockSpans.Span;
                maxY = span[0].TopY;

                if (column.BottomBlock.IsEmpty())
                {
                    minY = span[column.BlockSpans.Length - 1].EndY;
                }
                else
                {
                    minY = column.BottomBlock.FirstInstanceY;
                }
            }
            else
            {
                maxY = column.BottomBlock.FirstInstanceY;
                minY = maxY;
            }
        }

        private static void SetSections(int maxY, int minY, ConvertedApiChunk chunk, ScannedChunk output)
        {
            if (maxY == int.MinValue && minY == int.MaxValue)
            {
                output.BlockSections = Array.Empty<Section<Block>>();
                output.BiomeSections = Array.Empty<Section<string>>();

                return;
            }

            maxY = MathUtilities.FindSectionY(maxY);
            minY = MathUtilities.FindSectionY(minY);

            int count = maxY - minY + 1;

            Section<Block>[] blockSections = new Section<Block>[count];
            Section<string>[] biomeSections = new Section<string>[count];

            // Built once, then one lookup per Y. The previous version rescanned the whole
            // section list for every Y in the range, and enumerated it through
            // IEnumerable<ISection>, which allocated an enumerator on each of those scans.
            Span<short> blockAt = stackalloc short[SectionIndex.Size];
            Span<short> biomeAt = stackalloc short[SectionIndex.Size];

            IList<PaletteSection<Block>>? blocks = chunk.BlockState?.Sections;
            IList<PaletteSection<string>>? biomes = chunk.Biome?.Sections;

            SectionIndex.Build(blocks, blockAt);
            SectionIndex.Build(biomes, biomeAt);

            for (int y = 0; y < count; y++)
            {
                int sectionY = y + minY;
                if (sectionY < sbyte.MinValue || sectionY > sbyte.MaxValue) continue;

                sbyte yIndex = (sbyte)sectionY;

                int blockIndex = SectionIndex.IndexOf(blockAt, sectionY);
                if (blockIndex >= 0 && blocks is not null)
                {
                    blockSections[y] = new Section<Block>(yIndex, blocks[blockIndex].Palette);
                }

                int biomeIndex = SectionIndex.IndexOf(biomeAt, sectionY);
                if (biomeIndex >= 0 && biomes is not null)
                {
                    biomeSections[y] = new Section<string>(yIndex, biomes[biomeIndex].Palette);
                }
            }

            output.BlockSections = blockSections;
            output.BiomeSections = biomeSections;
        }
    }
}
