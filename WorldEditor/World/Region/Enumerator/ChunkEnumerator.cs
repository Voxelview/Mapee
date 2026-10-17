using CommonUtilities.Factory;

namespace WorldEditor
{
    public class ChunkEnumerator : IChunkEnumerator
    {
        public int RegionsPerTask { get; }
        public int TasksPerRegion { get; }
        public Func<IRegionStore> RegionStoreProvider { get; }

        private IChunkEnumeratorFromRegion[] _enumerators;

        public ChunkEnumerator(int regionsPerTask, int tasksPerRegion, Func<IRegionStore> regionStoreProvider, IFactory<int, IChunkEnumeratorFromRegion> factory)
        {
            if (regionsPerTask <= 0) throw new ArgumentOutOfRangeException(nameof(regionsPerTask));
            if (regionStoreProvider is null) throw new ArgumentNullException(nameof(regionStoreProvider));
            if (factory is null) throw new ArgumentNullException(nameof(factory));

            RegionsPerTask = regionsPerTask;
            TasksPerRegion = tasksPerRegion;
            RegionStoreProvider = regionStoreProvider;
            _enumerators = new IChunkEnumeratorFromRegion[regionsPerTask];

            for (int i = 0; i < _enumerators.Length; i++)
            {
                var enumerator = factory.Create(tasksPerRegion) ?? throw new InvalidOperationException("Factory.Create returned null for IChunkEnumeratorFromRegion");
                _enumerators[i] = enumerator;
            }
        }

        public virtual void Enumerate(Coords[] regions, IEnumerationBody body)
        {
            if (regions is null) throw new ArgumentNullException(nameof(regions));
            if (body is null) throw new ArgumentNullException(nameof(body));
            if (RegionsPerTask <= 0) return;

            int iterations = (int)Math.Ceiling(regions.Length / (float)RegionsPerTask);

            for (int i = 0; i < iterations; i++)
            {
                body.BeginCycle();

                int toExclusiveThis = Math.Min((i + 1) * RegionsPerTask, regions.Length);
                Parallel.For(i * RegionsPerTask, toExclusiveThis, index =>
                {
                    int iterator = index - (i * RegionsPerTask);

                    body.BeginReadingRegion(iterator, regions[index]);

                    ChunkEnumerateFromRegionArgs? args = null;
                    if (RegionStoreProvider.Invoke() is IChunkSourceRegionStore chunkSource)
                    {
                        chunkSource.EnumerateChunks(regions[index], TasksPerRegion, (r, chunk) => body.EndReadingChunk(iterator, r, chunk));
                    }
                    else
                    {
                        args = CreateArgs(regions[index], iterator);
                    }

                    if (args is not null)
                    {
                        var enumerator = _enumerators[iterator];
                        if (enumerator != null)
                        {
                            enumerator.Enumerate(args.Value, (r, chunk) => body.EndReadingChunk(iterator, r, chunk));
                        }
                    }

                    body.EndReadingRegion(iterator, regions[index]);
                });

                body.EndCycle();
            }
        }

        protected virtual ChunkEnumerateFromRegionArgs? CreateArgs(Coords regionCoords, int iterator)
        {
            IRegionStore? regionStore = RegionStoreProvider?.Invoke();
            if (regionStore is null) return null;

            if (!regionStore.GetData(regionCoords, out byte[]? buffer, out StorageFormat storageFormat)) 
            {
                return null;
            }

            if (buffer is null) 
            {
                return null;
            }

            return new ChunkEnumerateFromRegionArgs(buffer, CreateCoords(regionCoords.X, regionCoords.Z), storageFormat);
        }
        private static Coords[] CreateCoords(int regionX, int regionZ)
        {
            Coords[] chunksToRead = new Coords[1024];
            for (int i = 0; i < chunksToRead.Length; i++)
            {
                int x = regionX * 32 + i % 32, z = regionZ * 32 + i / 32;
                chunksToRead[i] = new Coords(x, z);
            }

            return chunksToRead;
        }
    }
}
