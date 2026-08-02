using System.Buffers;

namespace WorldEditor
{
    public class AlphaRegionStore : IRegionStore
    {
        public int Count => _store.Count;

        private readonly Dictionary<Coords, List<string>> _store = [];

        public IEnumerable<Coords> Itemize(string directory)
        {
            _store.Clear();

            bool ignoreDimensions = !Path.GetFileName(directory).Equals("DIM-1", StringComparison.OrdinalIgnoreCase);
            
            if (!Directory.Exists(directory)) return [];
            string[] files = Directory.GetFiles(directory, "c.*.*.dat", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (ignoreDimensions && Path.GetRelativePath(directory, file).StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Parser.TryParseChunkName(Path.GetFileName(file), out int chunkX, out int chunkZ))
                {
                    continue;
                }

                Coords regionCoords = new(
                    MathUtilities.FindSectionY(chunkX, 32),
                    MathUtilities.FindSectionY(chunkZ, 32));

                if (!_store.ContainsKey(regionCoords))
                {
                    _store[regionCoords] = [file];
                }
                else
                {
                    _store[regionCoords].Add(file);
                }
            }

            return _store.Keys;
        }

        public bool Exists(Coords coords)
        {
            return _store.ContainsKey(coords);
        }

        public bool GetData(Coords coords, out byte[]? buffer, out int length, out StorageFormat format)
        {
            if (!_store.TryGetValue(coords, out List<string>? chunkFiles) || chunkFiles is null)
            {
                buffer = null;
                length = 0;
                format = StorageFormat.Alpha;
                return false;
            }

            var chunkData = new Dictionary<int, byte[]>();
            var chunkTimestamps = new Dictionary<int, int>();

            Compression compression = new Compression();

            foreach (var file in chunkFiles)
            {
                Parser.TryParseChunkName(Path.GetFileName(file), out int chunkX, out int chunkZ);
                int localX = MathUtilities.NegMod(chunkX, 32);
                int localZ = MathUtilities.NegMod(chunkZ, 32);
                int index = localZ * 32 + localX;

                using (FileStream fileStream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] data = new byte[fileStream.Length];
                    fileStream.ReadExactly(data);
                    chunkData[index] = data;
                }

                int timestamp = (int)((DateTimeOffset)File.GetLastWriteTimeUtc(file))
                                    .ToUnixTimeSeconds();
                chunkTimestamps[index] = timestamp;
            }

            Span<byte> locationTable = stackalloc byte[4096];
            Span<byte> timestampTable = stackalloc byte[4096];
            var sectorsStream = new MemoryStream();

            int currentSector = 2;
            Span<byte> chunkHeader = stackalloc byte[5];

            for (int i = 0; i < 1024; i++)
            {
                if (!chunkData.TryGetValue(i, out byte[]? data))
                {
                    continue;
                }

                int dataLength = data.Length + 1;
                chunkHeader[0] = (byte)((dataLength >> 24) & 0xFF);
                chunkHeader[1] = (byte)((dataLength >> 16) & 0xFF);
                chunkHeader[2] = (byte)((dataLength >> 8) & 0xFF);
                chunkHeader[3] = (byte)(dataLength & 0xFF);
                chunkHeader[4] = (byte)CompressionType.GZip;

                sectorsStream.Write(chunkHeader);
                sectorsStream.Write(data);

                int chunkPayloadSize = chunkHeader.Length + data.Length;
                int sectorCount = (chunkPayloadSize + 4095) / 4096;
                int padding = sectorCount * 4096 - chunkPayloadSize;
                byte[] paddingBuffer = ArrayPool<byte>.Shared.Rent(padding);
                sectorsStream.Write(paddingBuffer, 0, padding);
                ArrayPool<byte>.Shared.Return(paddingBuffer);

                int locOffset = i * 4;
                locationTable[locOffset] = (byte)((currentSector >> 16) & 0xFF);
                locationTable[locOffset + 1] = (byte)((currentSector >> 8) & 0xFF);
                locationTable[locOffset + 2] = (byte)(currentSector & 0xFF);
                locationTable[locOffset + 3] = (byte)sectorCount;

                int ts = chunkTimestamps[i];
                int tsOffset = i * 4;
                timestampTable[tsOffset] = (byte)((ts >> 24) & 0xFF);
                timestampTable[tsOffset + 1] = (byte)((ts >> 16) & 0xFF);
                timestampTable[tsOffset + 2] = (byte)((ts >> 8) & 0xFF);
                timestampTable[tsOffset + 3] = (byte)(ts & 0xFF);

                currentSector += sectorCount;
            }

            // GetBuffer avoids the extra full copy ToArray would make; Length is the written extent.
            ReadOnlySpan<byte> sectorData = sectorsStream.GetBuffer().AsSpan(0, (int)sectorsStream.Length);

            length = 8192 + sectorData.Length;
            buffer = RegionBufferPool.Instance.Rent(length);

            locationTable.CopyTo(buffer);
            timestampTable.CopyTo(new Span<byte>(buffer, 4096, 4096));
            sectorData.CopyTo(new Span<byte>(buffer, 8192, sectorData.Length));

            format = StorageFormat.Alpha;
            return true;
        }
    }
}
