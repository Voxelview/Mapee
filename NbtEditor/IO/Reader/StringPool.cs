using System.Text;

namespace NbtEditor
{
    /// <summary>
    /// Deduplicates strings decoded from UTF-8 payloads. NBT chunk data repeats the same
    /// small vocabulary endlessly - tag names, block and biome ids, property values - so
    /// decoding each occurrence allocated hundreds of millions of identical short strings
    /// over a large load. Each reader owns one pool, so lookups need no synchronization.
    /// </summary>
    public sealed class StringPool
    {
        // Power of two. Large enough for tag names + block ids + property values of a
        // modern world; collisions just overwrite the slot.
        private const int Size = 4096;
        private const int MaxCachedLength = 64;

        private readonly string?[] _table = new string?[Size];

        public string GetOrAdd(ReadOnlySpan<byte> utf8)
        {
            if (utf8.Length == 0) return string.Empty;
            if (utf8.Length > MaxCachedLength) return Encoding.UTF8.GetString(utf8);

            int hash = Hash(utf8);
            ref string? slot = ref _table[hash & (Size - 1)];

            string? cached = slot;
            if (cached is not null && BytesEqualAscii(cached, utf8)) return cached;

            string created = Encoding.UTF8.GetString(utf8);
            slot = created;
            return created;
        }

        private static int Hash(ReadOnlySpan<byte> utf8)
        {
            // FNV-1a over 8/4-byte lanes instead of per byte. Collisions only cost a slot
            // overwrite here, so lane-wise folding is safe, and this hashes every decoded
            // string of a load.
            ulong hash = 14695981039346656037;
            while (utf8.Length >= 8)
            {
                hash = (hash ^ System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(utf8)) * 1099511628211;
                utf8 = utf8[8..];
            }
            if (utf8.Length >= 4)
            {
                hash = (hash ^ System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(utf8)) * 1099511628211;
                utf8 = utf8[4..];
            }
            for (int i = 0; i < utf8.Length; i++)
            {
                hash = (hash ^ utf8[i]) * 1099511628211;
            }

            return (int)(hash ^ (hash >> 32));
        }

        /// <summary>
        /// True only for an exact ASCII match. Multi-byte UTF-8 input always has more bytes
        /// than decoded chars, so the length check rejects it and it falls through to a
        /// fresh decode - never a false positive.
        /// </summary>
        private static bool BytesEqualAscii(string value, ReadOnlySpan<byte> utf8)
        {
            if (value.Length != utf8.Length) return false;

            for (int i = 0; i < utf8.Length; i++)
            {
                if (value[i] != utf8[i]) return false;
            }
            return true;
        }
    }
}
