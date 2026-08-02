using CommonUtilities.Factory;

namespace WorldEditor
{
    public class ChunkEnumerator : IChunkEnumerator
    {
        public int RegionsPerTask { get; }
        public Func<IRegionStore> RegionStoreProvider { get; }

        private IChunkEnumeratorFromRegion[] _enumerators;

        /// <summary>
        /// One reusable chunk-coordinate array per slot. Only the slot's own task writes to it, and it
        /// is fully rewritten per region, so this saves an 8 KB allocation on every region read.
        /// </summary>
        private readonly Coords[][] _chunkCoords;

        public ChunkEnumerator(int regionsPerTask, int tasksPerRegion, Func<IRegionStore> regionStoreProvider, IFactory<int, IChunkEnumeratorFromRegion> factory)
        {
            if (regionsPerTask <= 0) throw new ArgumentOutOfRangeException(nameof(regionsPerTask));
            if (regionStoreProvider is null) throw new ArgumentNullException(nameof(regionStoreProvider));
            if (factory is null) throw new ArgumentNullException(nameof(factory));

            RegionsPerTask = regionsPerTask;
            RegionStoreProvider = regionStoreProvider;
            _enumerators = new IChunkEnumeratorFromRegion[regionsPerTask];
            _chunkCoords = new Coords[regionsPerTask][];

            for (int i = 0; i < _enumerators.Length; i++)
            {
                var enumerator = factory.Create(tasksPerRegion) ?? throw new InvalidOperationException("Factory.Create returned null for IChunkEnumeratorFromRegion");
                _enumerators[i] = enumerator;
                _chunkCoords[i] = new Coords[1024];
            }
        }

        public static long BatchSlotTicks, RegionBusyTicks;

        public virtual void Enumerate(Coords[] regions, IEnumerationBody body)
        {
            if (regions is null) throw new ArgumentNullException(nameof(regions));
            if (body is null) throw new ArgumentNullException(nameof(body));
            if (RegionsPerTask <= 0) return;

            int iterations = (int)Math.Ceiling(regions.Length / (float)RegionsPerTask);

            for (int i = 0; i < iterations; i++)
            {
                body.BeginCycle();

                long batchStart = System.Diagnostics.Stopwatch.GetTimestamp();
                int toExclusiveThis = Math.Min((i + 1) * RegionsPerTask, regions.Length);
                Parallel.For(i * RegionsPerTask, toExclusiveThis,
                    WorkerPriority.Lower,
                    (index, _, priority) =>
                    {
                        int iterator = index - (i * RegionsPerTask);
                        ProcessRegion(regions[index], iterator, body);
                        return priority;
                    },
                    WorkerPriority.Restore);
                Interlocked.Add(ref BatchSlotTicks, (System.Diagnostics.Stopwatch.GetTimestamp() - batchStart) * (toExclusiveThis - i * RegionsPerTask));

                body.EndCycle();
            }
        }

        public virtual void Enumerate(Func<Coords[]> takeNext, IEnumerationBody body)
        {
            if (takeNext is null) throw new ArgumentNullException(nameof(takeNext));
            if (body is null) throw new ArgumentNullException(nameof(body));
            if (RegionsPerTask <= 0) return;

            body.BeginCycle();

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            Parallel.For(0, RegionsPerTask, slot =>
            {
                ThreadPriority priority = WorkerPriority.Lower();
                try
                {
                    while (true)
                    {
                        Coords[]? taken = takeNext();
                        if (taken is null || taken.Length == 0) break;

                        for (int i = 0; i < taken.Length; i++)
                        {
                            ProcessRegion(taken[i], slot, body);
                        }
                    }
                }
                finally
                {
                    WorkerPriority.Restore(priority);
                }
            });
            Interlocked.Add(ref BatchSlotTicks, (System.Diagnostics.Stopwatch.GetTimestamp() - start) * RegionsPerTask);

            body.EndCycle();
        }

        private void ProcessRegion(Coords regionCoords, int slot, IEnumerationBody body)
        {
            long regionStart = System.Diagnostics.Stopwatch.GetTimestamp();

            body.BeginReadingRegion(slot, regionCoords);

            ChunkEnumerateFromRegionArgs? args = CreateArgs(regionCoords, slot);
            if (args is not null)
            {
                try
                {
                    var enumerator = _enumerators[slot];
                    if (enumerator != null)
                    {
                        enumerator.Enumerate(args.Value, (r, chunk) => body.EndReadingChunk(slot, r, chunk));
                    }
                }
                finally
                {
                    // The store rents this; nothing holds a reference past Enumerate.
                    RegionBufferPool.Instance.Return(args.Value.RegionBuffer);
                }
            }

            body.EndReadingRegion(slot, regionCoords);
            Interlocked.Add(ref RegionBusyTicks, System.Diagnostics.Stopwatch.GetTimestamp() - regionStart);
        }

        protected virtual ChunkEnumerateFromRegionArgs? CreateArgs(Coords regionCoords, int iterator)
        {
            IRegionStore? regionStore = RegionStoreProvider?.Invoke();
            if (regionStore is null) return null;

            if (!regionStore.GetData(regionCoords, out byte[]? buffer, out int length, out StorageFormat storageFormat))
            {
                return null;
            }

            if (buffer is null)
            {
                return null;
            }

            return new ChunkEnumerateFromRegionArgs(buffer, length, FillCoords(_chunkCoords[iterator], regionCoords.X, regionCoords.Z), storageFormat);
        }
        private static Coords[] FillCoords(Coords[] chunksToRead, int regionX, int regionZ)
        {
            for (int i = 0; i < chunksToRead.Length; i++)
            {
                int x = regionX * 32 + i % 32, z = regionZ * 32 + i / 32;
                chunksToRead[i] = new Coords(x, z);
            }

            return chunksToRead;
        }
    }
}
