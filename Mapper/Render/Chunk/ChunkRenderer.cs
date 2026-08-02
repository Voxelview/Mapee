using CommonUtilities.Factory;
using MapScanner;
using WorldEditor;

namespace Mapper
{
    public class ChunkRenderer : IChunkRenderer<ChunkRenderArgs>
    {
        public IColumnRenderer<ColumnArgs> ColumnRenderer { get; set; }
        public IFactory<ChunkRenderArgs, IBlockController> BlockControllerFactory { get; set; }

        public ChunkRenderer(IColumnRenderer<ColumnArgs> columnRenderer, IFactory<ChunkRenderArgs, IBlockController> blockControllerFactory)
        {
            ColumnRenderer = columnRenderer;
            BlockControllerFactory = blockControllerFactory;
        }

        public void Render(ChunkRenderArgs input, ICanvas canvas)
        {
            IBlockController controller = BlockControllerFactory.Create(input);
            IScannedChunk chunk = input.ScannedChunk;

            // Positions sharing a scanned column render to the same color whenever their step
            // deltas are all zero: the step multiplier is the only position-dependent input,
            // for zero deltas it collapses to a per-block constant, and equal-column positions
            // share their step base Y (step chunks are expanded from the same unique values).
            // Flat terrain repeats a handful of columns 256 times, so cache by unique index.
            int uniqueCount = chunk.UniqueColumns.Count;
            Span<VecRgb> flatColor = stackalloc VecRgb[uniqueCount];
            Span<bool> flatKnown = stackalloc bool[uniqueCount];

            for (int i = 0; i < 256; i++)
            {
                int unique = chunk.Indexes[i];
                ScannedColumn column = chunk.UniqueColumns[unique];
                if (column.Type == ColumnType.Empty) continue;

                int x = i % 16, z = i / 16;

                Step step = controller.GetStep(new Coords(x, z));
                bool flat = step.XPos == 0 && step.XNeg == 0 && step.ZPos == 0 && step.ZNeg == 0;

                VecRgb color;
                if (flat && flatKnown[unique])
                {
                    color = flatColor[unique];
                }
                else
                {
                    color = ColumnRenderer.Render(new ColumnArgs(column, new Coords(x, z), controller));

                    if (flat)
                    {
                        flatColor[unique] = color;
                        flatKnown[unique] = true;
                    }
                }

                if (color.IsEmpty()) continue;
                canvas.SetPixel(chunk.Coords.X * 16 + x, chunk.Coords.Z * 16 + z, color.Clamp());
            }

            controller.Dispose();
        }
    }
}
