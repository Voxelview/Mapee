using CommonUtilities.Collections.Simple;
using NbtEditor;

namespace WorldEditor
{
    public class HeightmapReader : IObjectReader<ObjectReadParamter, HeightmapCollection?>
    {
        public VersionList<string[]> Versions { get; set; }

        public bool FilterHeightmaps { get; set; }
        public IList<string> FilteredHeightmaps { get; set; }

        public HeightmapReader()
        {
            Versions = new VersionList<string[]>
            {
                { Version.Snapshot_17w47a, Version.Snapshot_21w42a, new[] { "Level", "Heightmaps" } },
                { Version.Snapshot_21w43a, Version.Newest, new[] { "Heightmaps" } }
            };

            FilterHeightmaps = false;
            FilteredHeightmaps = new List<string>() { "WORLD_SURFACE" };
        }

        public HeightmapCollection? Read(ObjectReadParamter input)
        {
            if (!Versions.TryRetrieveValue(input.Version, out string[]? path)) return null;
            if (!input.Level.TryGetChild(out Tag heightmapTag, path)) return null;

            HeightmapCollection output = new()
            {
                DataTag = input.KeepDataTag ? heightmapTag : null
            };

            if (heightmapTag is not CompoundTag heightmap) return null;

            if (FilterHeightmaps)
            {
                foreach (string name in FilteredHeightmaps)
                {
                    if (heightmap.TryGetValue(name, out Tag? tag))
                    {
                        output.Heightmaps.Add(name, CreateHeightmap(tag, input.Version));
                    }
                }
            }
            else
            {
                for (int i = 0; i < heightmap.Count; i++)
                {
                    KeyValueEntry<string, Tag> pair = heightmap.EntryAt(i);
                    output.Heightmaps.Add(pair.Key, CreateHeightmap(pair.Value, input.Version));
                }
            }

            return output;
        }

        /// <summary>
        /// Lockers are stateless besides their reader, and the readers are cached statics,
        /// so every heightmap of a version can share one instance instead of allocating a
        /// locker per heightmap per chunk.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Version, HeightmapLocker> SharedLockers = new();

        private static HeightmapCollection.Heightmap CreateHeightmap(long[] indexes, Version version)
        {
            return new HeightmapCollection.Heightmap(indexes)
            {
                Locker = SharedLockers.GetOrAdd(version, static v => new HeightmapLocker()
                {
                    Reader = ChunkUtilities.GetBlockStateReader(v)
                })
            };
        }
    }
}
