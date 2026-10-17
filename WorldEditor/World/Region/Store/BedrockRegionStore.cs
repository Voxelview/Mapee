namespace WorldEditor
{
    // Bedrock has no region files. Chunks are found by scanning the world database once,
    // then grouped into the same 32x32 regions Java uses so the rest of Mapee can treat them alike
    public class BedrockRegionStore : IChunkSourceRegionStore, IDisposable
    {
        public int Dimension { get; }
        public int Count => _regions.Count;

        public BedrockChunkReader ChunkReader { get; set; } = new();
        public ILogger<ChunkError> ErrorLogger { get; set; } = new ConsoleWriteLogger<ChunkError>();

        private LevelDbReader? _db;
        private readonly Dictionary<Coords, List<Coords>> _regions = [];

        public BedrockRegionStore(int dimension)
        {
            Dimension = dimension;
        }

        // directory is the world folder, the one holding level.dat and db
        public IEnumerable<Coords> Itemize(string directory)
        {
            _regions.Clear();
            _db?.Dispose();
            _db = null;

            string dbDirectory = Path.Combine(directory, "db");
            if (!File.Exists(Path.Combine(dbDirectory, "CURRENT"))) return [];

            _db = new LevelDbReader(dbDirectory);
            _db.ForEachKey(key =>
            {
                if (!BedrockKey.TryParse(key, out BedrockKey parsed) || parsed.Dimension != Dimension) return;
                if (parsed.Tag != BedrockChunkTag.Version && parsed.Tag != BedrockChunkTag.LegacyVersion) return;

                Coords region = new(parsed.X >> 5, parsed.Z >> 5);
                if (!_regions.TryGetValue(region, out List<Coords>? chunks)) _regions[region] = chunks = [];
                chunks.Add(new Coords(parsed.X, parsed.Z));
            });

            return _regions.Keys;
        }

        public bool Exists(Coords coords)
        {
            return _regions.ContainsKey(coords);
        }

        public bool GetData(Coords coords, out byte[]? buffer, out StorageFormat format)
        {
            buffer = null;
            format = StorageFormat.Bedrock;
            return false;
        }

        public void EnumerateChunks(Coords region, int tasks, Action<int, IChunk> body)
        {
            if (_db is null || !_regions.TryGetValue(region, out List<Coords>? chunks)) return;
            LevelDbReader db = _db;

            ParallelUtilities.BufferedFor(0, chunks.Count, tasks, (index, r) =>
            {
                Coords coords = chunks[index];
                try
                {
                    IDictionary<byte[], byte[]> records = db.GetByPrefix(BedrockKey.ChunkPrefix(coords.X, coords.Z, Dimension));
                    ApiChunk? chunk = ChunkReader.Read(coords.X, coords.Z, Dimension, records);
                    if (chunk is not null) body(r, chunk);
                }
                catch (Exception e)
                {
                    ErrorLogger.Log(new ChunkError(e, coords));
                }
            });
        }

        public void Dispose()
        {
            _db?.Dispose();
            _db = null;
            GC.SuppressFinalize(this);
        }
    }
}
