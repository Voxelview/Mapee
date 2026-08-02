using MapScanner;
using WorldEditor;

namespace Mapper
{
    public class ScannedRegion
    {
        public Coords Coords { get; set; }

        /// <summary>
        /// One slot per chunk position in the region, or null where no chunk was scanned.
        /// This replaces a synchronized list: eight scan workers add to a region at once and
        /// the renderer reads every chunk back, so the shared lock was among the hottest
        /// monitors in a load. Slot writes need no lock because no two chunks share a slot.
        /// </summary>
        public IScannedChunk?[] Chunks { get; }

        public ScannedRegion(Coords coords)
        {
            Coords = coords;
            Chunks = new IScannedChunk?[1024];
        }

        public void Add(IScannedChunk chunk)
        {
            Chunks[(chunk.Coords.X & 31) + (chunk.Coords.Z & 31) * 32] = chunk;
        }
    }
}
