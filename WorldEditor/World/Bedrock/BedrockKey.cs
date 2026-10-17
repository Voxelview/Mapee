using System.Buffers.Binary;

namespace WorldEditor
{
    public enum BedrockChunkTag : byte
    {
        Data3D = 43,
        Version = 44,
        Data2D = 45,
        SubChunkPrefix = 47,
        LegacyTerrain = 48,
        BlockEntity = 49,
        Entity = 50,
        PendingTicks = 51,
        BiomeState = 53,
        FinalizedState = 54,
        LegacyVersion = 118
    }

    // Chunk keys are x (int32), z (int32), an int32 dimension unless it is the overworld,
    // a tag byte, and for sub-chunks a signed section Y byte
    public readonly struct BedrockKey
    {
        public int X { get; init; }
        public int Z { get; init; }
        public int Dimension { get; init; }
        public BedrockChunkTag Tag { get; init; }
        public sbyte SubChunkY { get; init; }

        public static byte[] ChunkPrefix(int x, int z, int dimension)
        {
            byte[] output = new byte[dimension == 0 ? 8 : 12];
            BinaryPrimitives.WriteInt32LittleEndian(output, x);
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4), z);
            if (dimension != 0) BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(8), dimension);
            return output;
        }

        public static bool TryParse(ReadOnlySpan<byte> key, out BedrockKey output)
        {
            output = default;

            int tagPosition;
            int dimension = 0;
            switch (key.Length)
            {
                case 9:
                case 10:
                    tagPosition = 8;
                    break;
                case 13:
                case 14:
                    tagPosition = 12;
                    dimension = BinaryPrimitives.ReadInt32LittleEndian(key[8..]);
                    break;
                default:
                    return false;
            }

            BedrockChunkTag tag = (BedrockChunkTag)key[tagPosition];
            bool hasSubChunk = key.Length == tagPosition + 2;

            // Non-chunk keys such as "~local_player" can share these lengths, so check the shape too
            if (hasSubChunk != (tag == BedrockChunkTag.SubChunkPrefix)) return false;
            if (tag < BedrockChunkTag.Data3D || (tag > (BedrockChunkTag)65 && tag != BedrockChunkTag.LegacyVersion)) return false;
            if (dimension < 0 || dimension > 2) return false;

            output = new BedrockKey()
            {
                X = BinaryPrimitives.ReadInt32LittleEndian(key),
                Z = BinaryPrimitives.ReadInt32LittleEndian(key[4..]),
                Dimension = dimension,
                Tag = tag,
                SubChunkY = hasSubChunk ? (sbyte)key[tagPosition + 1] : (sbyte)0
            };
            return true;
        }
    }
}
