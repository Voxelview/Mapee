using NbtEditor;

namespace WorldEditor
{
    public static class ChunkUtilities
    {
        private static readonly string[] _coordinateXPath = new string[] { "Level", "xPos" };
        private static readonly string[] _coordinateZPath = new string[] { "Level", "zPos" };

        public static VersionList<string[]> GetSectionVersions()
        {
            VersionList<string[]> output = new VersionList<string[]>();
            output.Add(Version.Post_1_1, Version.Snapshot_21w42a, new[] { "Level", "Sections" });
            output.Add(Version.Snapshot_21w43a, Version.Newest, new[] { "sections" });

            return output;
        }
        public static VersionList<BlockStatePath> GetBlockStateVersions()
        {
            VersionList<BlockStatePath> output = new VersionList<BlockStatePath>();
            output.Add(Version.Post_1_1, Version.Snapshot_21w38a, new BlockStatePath(new[] { "BlockStates" }, new[] { "Palette" }));
            output.Add(Version.Snapshot_21w39a, Version.Newest, new BlockStatePath(new[] { "block_states", "data" }, new[] { "block_states", "palette" }));

            return output;
        }
        // Both readers are stateless, and this is called per heightmap/section of every
        // chunk - allocating fresh instances added hundreds of MB of garbage per load.
        private static readonly IBlockStateReader _legacyBlockStateReader = new BlockStateReader();
        private static readonly IBlockStateReader _simpleBlockStateReader = new SimpleBlockStateReader();

        public static IBlockStateReader GetBlockStateReader(Version version)
        {
            if (version < Version.Snapshot_20w17a)
            {
                return _legacyBlockStateReader;
            }

            return _simpleBlockStateReader;
        }
        public static IBlockStateWriter GetBlockStateWriter(Version version)
        {
            if (version < Version.Snapshot_20w17a)
            {
                return new BlockStateWriter();
            }

            return new SimpleBlockStateWriter();
        }
        public static void ReadCoordinates(CompoundTag level, Version version, IChunk output)
        {
            if (version < Version.Snapshot_21w43a)
            {
                output.X = level.GetChild(_coordinateXPath);
                output.Z = level.GetChild(_coordinateZPath);
            }
            else
            {
                output.X = level["xPos"] ?? 0;
                output.Z = level["zPos"] ?? 0;
            }
        }
        public static int CalculateHashCode(byte blockState, byte data)
        {
            return blockState << 8 | data;
        }
        public static Coords FindRegionFromChunk(int x, int z)
        {
            return new Coords((x - MathUtilities.NegMod(x, 32)) / 32, (z - MathUtilities.NegMod(z, 32)) / 32);
        }
    }
}
