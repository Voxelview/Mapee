using CommonUtilities.Collections.Simple;
using System.Collections.Generic;
using WorldEditor;

namespace MapScanner
{
    public class ScannedChunk : IScannedChunk
    {
        public Coords Coords { get; set; }

        public IList<ScannedColumn> UniqueColumns { get; set; }
        public byte[] Indexes { get; set; }

        public Section<Block>[]? BlockSections { get; set; }
        public Section<string>[]? BiomeSections { get; set; }

        public ScannedChunk(Coords coords)
        {
            Coords = coords;

            // Starts well below the 256-column worst case: most chunks deduplicate to far
            // fewer unique columns, and the list doubles on demand. Pre-sizing to 256 cost
            // ~8 KB per chunk, which was over 12% of all allocation in a large load.
            UniqueColumns = new SimpleList<ScannedColumn>(64);
            Indexes = new byte[256];
        }

        public ScannedColumn GetColumn(int index)
        {
            return UniqueColumns[Indexes[index]];
        }
    }
}
