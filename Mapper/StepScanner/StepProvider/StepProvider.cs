using System.Collections.Concurrent;
using WorldEditor;

namespace Mapper
{
    public class StepProvider : IStepProvider
    {
        /// <summary>
        /// Step chunks stored in one slab per region, indexed by the chunk's position in the
        /// region. Keying the dictionary per chunk meant one locked insert per chunk from
        /// every scan thread at once - the contention alone was ~13% of all CPU in a large
        /// load. A slab is inserted once per region; chunk writes are plain array stores,
        /// safe because every chunk owns a distinct slot.
        /// </summary>
        private readonly ConcurrentDictionary<Coords, StepChunk?[]> _regions;

        public StepProvider()
        {
            _regions = new ConcurrentDictionary<Coords, StepChunk?[]>();
        }

        public void Add(int x, int z, StepChunk chunk)
        {
            StepChunk?[] slab = _regions.GetOrAdd(new Coords(x >> 5, z >> 5), static _ => new StepChunk?[1024]);
            slab[(x & 31) + (z & 31) * 32] = chunk;
        }
        public void Remove(int x, int z)
        {
            if (_regions.TryGetValue(new Coords(x >> 5, z >> 5), out StepChunk?[]? slab))
            {
                slab[(x & 31) + (z & 31) * 32] = null;
            }
        }

        /// <summary>Drops a whole region's slab once no future render can query it.</summary>
        public void RemoveRegion(Coords regionCoords)
        {
            _regions.TryRemove(regionCoords, out _);
        }

        public void Clear()
        {
            _regions.Clear();
        }

        public short[]? ProvideStepStrip(int x, int z, Direction direction)
        {
            short[]? chunk = ProvideStepChunk(x, z);
            if (chunk is null) return null;

            short[] output = new short[16];
            switch (direction)
            {
                case Direction.East:
                    for (int i = 0; i < 16; i++)
                    {
                        output[i] = chunk[i * 16 + 15];
                    }
                    break;
                case Direction.West:
                    for (int i = 0; i < 16; i++)
                    {
                        output[i] = chunk[i * 16];
                    }
                    break;
                case Direction.South:
                    for (int i = 0; i < 16; i++)
                    {
                        output[i] = chunk[16 * 15 + i];
                    }
                    break;
                case Direction.North:
                    for (int i = 0; i < 16; i++)
                    {
                        output[i] = chunk[i];
                    }
                    break;
            }

            return output;
        }
        public short[]? ProvideStepChunk(int x, int z)
        {
            if (!_regions.TryGetValue(new Coords(x >> 5, z >> 5), out StepChunk?[]? slab)) return null;
            return slab[(x & 31) + (z & 31) * 32]?.Steps;
        }
    }
}
