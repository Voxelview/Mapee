using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace WorldEditor
{
    // A read-only sorted string table (.ldb/.sst). Safe to read from several threads at once
    public sealed class LevelDbTable : IDisposable
    {
        public ulong Number { get; }

        private const int FOOTER_SIZE = 48;
        private const ulong MAGIC = 0xdb4775248b80fb57;
        private const int MAX_CACHED_BLOCKS = 256;

        private readonly SafeFileHandle _handle;
        private readonly (byte[] LastKey, long Offset, int Size)[] _index;
        private readonly ConcurrentDictionary<long, byte[]> _cache = new();

        public LevelDbTable(string file, ulong number)
        {
            Number = number;
            _handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess);

            long length = RandomAccess.GetLength(_handle);
            byte[] footer = new byte[FOOTER_SIZE];
            RandomAccess.Read(_handle, footer, length - FOOTER_SIZE);

            if (BinaryPrimitives.ReadUInt64LittleEndian(footer.AsSpan(40)) != MAGIC)
            {
                throw new InvalidDataException($"{file} is not a LevelDB table");
            }

            int position = 0;
            LevelDbUtilities.ReadVarint64(footer, ref position); // metaindex offset
            LevelDbUtilities.ReadVarint64(footer, ref position); // metaindex size
            long indexOffset = (long)LevelDbUtilities.ReadVarint64(footer, ref position);
            int indexSize = (int)LevelDbUtilities.ReadVarint64(footer, ref position);

            List<(byte[], long, int)> index = [];
            foreach (var (key, value) in ReadEntries(ReadBlock(indexOffset, indexSize)))
            {
                int valuePosition = 0;
                long offset = (long)LevelDbUtilities.ReadVarint64(value, ref valuePosition);
                int size = (int)LevelDbUtilities.ReadVarint64(value, ref valuePosition);
                index.Add((key, offset, size));
            }
            _index = [.. index];
        }

        // Calls body for every entry whose user key starts with prefix, in key order
        public void ScanPrefix(ReadOnlySpan<byte> prefix, LevelDbEntryHandler body)
        {
            int lo = 0, hi = _index.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (LevelDbUtilities.Compare(LevelDbUtilities.UserKey(_index[mid].LastKey), prefix) < 0) lo = mid + 1;
                else hi = mid;
            }

            for (int i = lo; i < _index.Length; i++)
            {
                foreach (var (key, value) in ReadEntries(GetBlock(i)))
                {
                    ReadOnlySpan<byte> userKey = LevelDbUtilities.UserKey(key);
                    int comparison = LevelDbUtilities.ComparePrefix(userKey, prefix);
                    if (comparison < 0) continue;
                    if (comparison > 0) return;

                    LevelDbUtilities.ParseTrailer(key, out ulong sequence, out bool isDeletion);
                    body(userKey, sequence, isDeletion, value);
                }
            }
        }
        public void ScanAll(LevelDbEntryHandler body)
        {
            for (int i = 0; i < _index.Length; i++)
            {
                foreach (var (key, value) in ReadEntries(ReadBlock(_index[i].Offset, _index[i].Size)))
                {
                    LevelDbUtilities.ParseTrailer(key, out ulong sequence, out bool isDeletion);
                    body(LevelDbUtilities.UserKey(key), sequence, isDeletion, value);
                }
            }
        }

        private byte[] GetBlock(int indexEntry)
        {
            var (_, offset, size) = _index[indexEntry];
            if (_cache.TryGetValue(offset, out byte[]? block)) return block;

            block = ReadBlock(offset, size);
            if (_cache.Count >= MAX_CACHED_BLOCKS) _cache.Clear();
            _cache[offset] = block;
            return block;
        }
        private byte[] ReadBlock(long offset, int size)
        {
            // Each block is followed by a 1 byte compression type and a 4 byte checksum
            byte[] raw = new byte[size + 1];
            RandomAccess.Read(_handle, raw, offset);
            return LevelDbUtilities.Decompress(raw, 0, size, raw[size]);
        }

        private static List<(byte[] Key, byte[] Value)> ReadEntries(byte[] block)
        {
            ReadOnlySpan<byte> data = block;
            int restartCount = BinaryPrimitives.ReadInt32LittleEndian(data[^4..]);
            int end = data.Length - 4 - restartCount * 4;

            List<(byte[], byte[])> output = [];
            byte[] lastKey = [];

            int position = 0;
            while (position < end)
            {
                int shared = LevelDbUtilities.ReadVarint32(data, ref position);
                int nonShared = LevelDbUtilities.ReadVarint32(data, ref position);
                int valueLength = LevelDbUtilities.ReadVarint32(data, ref position);

                byte[] key = new byte[shared + nonShared];
                lastKey.AsSpan(0, shared).CopyTo(key);
                data.Slice(position, nonShared).CopyTo(key.AsSpan(shared));
                position += nonShared;

                output.Add((key, data.Slice(position, valueLength).ToArray()));
                position += valueLength;
                lastKey = key;
            }

            return output;
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }

    public delegate void LevelDbEntryHandler(ReadOnlySpan<byte> userKey, ulong sequence, bool isDeletion, byte[] value);
}
