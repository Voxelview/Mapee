using System.Buffers.Binary;
using System.IO.Compression;

namespace WorldEditor
{
    public static class LevelDbUtilities
    {
        public static ulong ReadVarint64(ReadOnlySpan<byte> data, ref int position)
        {
            ulong output = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                byte b = data[position++];
                output |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return output;
            }

            throw new InvalidDataException("Malformed varint");
        }
        public static int ReadVarint32(ReadOnlySpan<byte> data, ref int position)
        {
            return checked((int)ReadVarint64(data, ref position));
        }
        public static byte[] ReadLengthPrefixed(ReadOnlySpan<byte> data, ref int position)
        {
            int length = ReadVarint32(data, ref position);
            byte[] output = data.Slice(position, length).ToArray();
            position += length;
            return output;
        }

        // An internal key is the user key followed by 8 bytes holding (sequence << 8 | type)
        public static ReadOnlySpan<byte> UserKey(ReadOnlySpan<byte> internalKey)
        {
            return internalKey[..^8];
        }
        public static void ParseTrailer(ReadOnlySpan<byte> internalKey, out ulong sequence, out bool isDeletion)
        {
            ulong trailer = BinaryPrimitives.ReadUInt64LittleEndian(internalKey[^8..]);
            sequence = trailer >> 8;
            isDeletion = (trailer & 0xFF) == 0;
        }

        public static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            return left.SequenceCompareTo(right);
        }
        // Compares only the first prefix.Length bytes of key, so every key starting with prefix compares equal
        public static int ComparePrefix(ReadOnlySpan<byte> key, ReadOnlySpan<byte> prefix)
        {
            return key[..Math.Min(key.Length, prefix.Length)].SequenceCompareTo(prefix);
        }

        public static byte[] Decompress(byte[] data, int offset, int length, byte compressionType)
        {
            switch (compressionType)
            {
                case 0:
                    return data.AsSpan(offset, length).ToArray();
                case 2:
                    using (MemoryStream input = new(data, offset, length))
                    using (ZLibStream zlib = new(input, CompressionMode.Decompress))
                    {
                        return ReadToEnd(zlib);
                    }
                case 4:
                    using (MemoryStream input = new(data, offset, length))
                    using (DeflateStream deflate = new(input, CompressionMode.Decompress))
                    {
                        return ReadToEnd(deflate);
                    }
                default:
                    throw new NotSupportedException($"Unsupported LevelDB block compression {compressionType}");
            }
        }
        private static byte[] ReadToEnd(Stream stream)
        {
            using MemoryStream output = new();
            stream.CopyTo(output);
            return output.ToArray();
        }
    }
}
