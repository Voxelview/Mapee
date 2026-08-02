using CommonUtilities.Pool;
using NbtEditor;

namespace WorldEditor
{
    public class ChunkEnumeratorFromRegion : IChunkEnumeratorFromRegion
    {
        // 4 KB location table + 4 KB timestamp table. Chunk payloads start after both.
        private const int HeaderLength = 8192;

        public virtual int TasksPerRegion { get; } = 8;
        public virtual IObjectReader<ChunkParamater, IChunk?>? ChunkReader { get; set; }
        public virtual ILogger<ChunkError> ErrorLogger { get; set; }
        public virtual ICompression Compression { get; set; }

        public virtual IPool<int, byte[]> DecompressedArrayPool { get; set; }
        public virtual ITagDeserializer TagDeserializer { get; }

        public ChunkEnumeratorFromRegion(int tasksPerRegion = 8)
        {
            TasksPerRegion = tasksPerRegion;

            ChunkReader = new ApiChunkReader();
            ErrorLogger = new ConsoleWriteLogger<ChunkError>();
            Compression = new Compression();

            DecompressedArrayPool = new IndexedObjectPool<byte[]>(TasksPerRegion, i => new byte[1024 * 512]);
            TagDeserializer = new PooledTagDeserializer(TasksPerRegion);
        }

        public static long DecompressTicks, DeserializeTicks, ChunkReadTicks, BodyTicks;

        public virtual void Enumerate(ChunkEnumerateFromRegionArgs args, Action<int, IChunk> body)
        {
            if (args.DataLength < HeaderLength) return;

            ParallelUtilities.BufferedFor(0, args.ChunksToRead.Length, TasksPerRegion, (index, r) =>
            {
                try
                {
                    int pos = MathUtilities.NegMod(args.ChunksToRead[index].X, 32) + MathUtilities.NegMod(args.ChunksToRead[index].Z, 32) * 32;
                    int chunkOffset = Parser.ParseInt24(args.RegionBuffer, pos * 4) * 4096;
                    if (chunkOffset < HeaderLength || chunkOffset + 5 > args.DataLength) return;

                    int chunkSize = Parser.ParseInt32(args.RegionBuffer, chunkOffset) - 1;
                    // The buffer is rented and longer than the region, so a truncated or corrupt entry
                    // would otherwise read whichever region used this buffer last.
                    if (chunkSize <= 0 || chunkOffset + 5 + chunkSize > args.DataLength) return;

                    ArraySlice<byte> input = new(args.RegionBuffer, chunkOffset + 5, chunkSize);
                    ArraySlice<byte> output = new(DecompressedArrayPool.Provide(r));
                    CompressionType compressionType = (CompressionType)args.RegionBuffer[chunkOffset + 4];

                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    int red = Compression.Compress(input, output, compressionType);
                    if (red < 0) return;

                    long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    CompoundTag? level = TagDeserializer.Deserialize(output, r);
                    if (level is null) return;

                    long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                    ChunkParamater chunkParameter = new(level, args.StorageFormat);

                    IChunk? chunk = ChunkReader?.Read(chunkParameter);
                    if (chunk is null) return;

                    chunk.X = args.ChunksToRead[index].X;
                    chunk.Z = args.ChunksToRead[index].Z;
                    chunk.LastModified = Parser.ParseInt32(args.RegionBuffer, pos * 4 + 4096);

                    long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                    body?.Invoke(r, chunk);
                    long t4 = System.Diagnostics.Stopwatch.GetTimestamp();

                    Interlocked.Add(ref DecompressTicks, t1 - t0);
                    Interlocked.Add(ref DeserializeTicks, t2 - t1);
                    Interlocked.Add(ref ChunkReadTicks, t3 - t2);
                    Interlocked.Add(ref BodyTicks, t4 - t3);
                }
                catch (Exception e)
                {
                    ErrorLogger.Log(new ChunkError(e, args.ChunksToRead[index]));
                }
            });
        }
    }
}
