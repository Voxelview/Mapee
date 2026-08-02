using System;
using WorldEditor;

namespace MapScanner
{
    public class HeightmapLevelProvider : ILevelProvider
    {
        public HeightmapCollection.Heightmap Heightmap { get; set; }

        /// <summary>Kept across chunks when the provider is pooled; heightmaps are all 16x16.</summary>
        private short[]? _values;
        private bool _unlocked;

        public HeightmapLevelProvider(HeightmapCollection.Heightmap heightmap)
        {
            Heightmap = heightmap;
        }

        /// <summary>Re-points a pooled provider at the next chunk's heightmap.</summary>
        public void Reset(HeightmapCollection.Heightmap heightmap)
        {
            Heightmap = heightmap;
            _unlocked = false;
        }

        public short Provide(int x, int z)
        {
            if (!_unlocked)
            {
                if (Heightmap.Locker is null)
                {
                    _values ??= new short[256];
                    Array.Clear(_values);
                }
                else
                {
                    if (_values is null || _values.Length < Heightmap.Locker.UnlockedArrayLength)
                    {
                        _values = new short[Heightmap.Locker.UnlockedArrayLength];
                    }
                    Heightmap.Unlock(_values);
                }

                _unlocked = true;
            }

            return (short)(_values![x + z * 16] - 1);
        }
    }
}
