using WorldEditor;

namespace Mapper
{
    public class WorldMapper
    {
        public MapperPack MapperPack { get; }

        public IChunkEnumerator ChunkEnumerator { get; }
        protected WorldMapperEnumerationBody EnumerationBody { get; }

        public Action<Coords, ICanvas>? RegionRendered
        {
            get => EnumerationBody.RegionRendered;
            set => EnumerationBody.RegionRendered = value;
        }
        public SceneInfo CurrentScene { get; protected set; }
        public IQueue<Coords> Queue { get; }

        private Action? _invoke;
        private bool _newInvoke = false;
        private object _invokeLock = new();

        /// <summary>
        /// Regions read concurrently. Wider, shallower batches beat the old 8x8 split: more
        /// independent regions per cycle smooths out the slow-region tail at each batch
        /// barrier, and fewer decode tasks per region cost less coordination. 16x4 measured
        /// fastest (with 24x3/24x4 equal within noise) on an 8-core machine.
        /// </summary>
        public const int REGIONS_IN_PARALLEL = 16;

        /// <summary>Chunks decoded concurrently within one region.</summary>
        public const int CHUNKS_IN_PARALLEL = 4;

        /// <summary>
        /// Distinct worker slots, and so the size every per-slot pool has to cover. Anything
        /// pooling by slot index must use this rather than its own constant - the scan pools
        /// are indexed by <c>regionIndex * CHUNKS_IN_PARALLEL + chunkIndex</c>.
        /// </summary>
        public const int SCAN_SLOTS = REGIONS_IN_PARALLEL * CHUNKS_IN_PARALLEL;

        public WorldMapper(MapperPack mapperPack)
        {
            MapperPack = mapperPack;

            ChunkEnumeratorFromRegionFactory factory = new(index => MapperPack.ChunkReader);
            ChunkEnumerator = new ChunkEnumerator(REGIONS_IN_PARALLEL, CHUNKS_IN_PARALLEL, () => CurrentScene.RegionStore, factory);

            EnumerationBody = new WorldMapperEnumerationBody(MapperPack, REGIONS_IN_PARALLEL, CHUNKS_IN_PARALLEL);
            Queue = new SynchronizedQueue();

            InitializeThread();
        }

        public virtual void SetScene(SceneInfo scene)
        {
            CurrentScene = scene;

            Stop();
            IRegionStore regionStore = scene.RegionStore;
            EnumerationBody.RegionExists = regionStore is null ? null : regionStore.Exists;
            EnumerationBody.ResetScene();
        }
        public void Stop()
        {
            Queue.ReplaceWith(ReadOnlyMemory<Coords>.Empty);
        }

        private void InitializeThread()
        {
            new Thread(() =>
            {
                while (true)
                {
                    EnumerateThroughCache();
                    Thread.Sleep(5);
                }
            })
            {
                IsBackground = true,
                // Loading must never outcompete the UI thread for time slices.
                Priority = ThreadPriority.BelowNormal
            }.Start();
        }
        /// <summary>
        /// After a load of at least this many regions, one aggressive collection returns the
        /// load's garbage to the OS - the periodic background GC is deliberately gentle and
        /// would otherwise leave gigabytes of dead heap sitting in the working set. Small
        /// pans stay below the threshold and never trigger it.
        /// </summary>
        private const int COLLECT_AFTER_REGIONS = 64;

        /// <summary>
        /// The collection waits for half a second of queue silence, so a pan that keeps
        /// feeding the queue is never interrupted by it.
        /// </summary>
        private static readonly long CollectDelayTicks = System.Diagnostics.Stopwatch.Frequency / 2;

        private int _regionsSinceIdle;
        private long _collectDeadline;

        protected virtual void EnumerateThroughCache()
        {
            // Batches keep scanning and rendering in coarse alternating phases. The streamed
            // Enumerate overload (pull callback, no per-batch join) removes the straggler wait
            // entirely, but measured slower end to end: with every stage running at once the
            // same CPU work spreads thinner and the phase-local cache locality is lost.
            while (Queue.Count > 0)
            {
                Coords[] regions = Queue.TakeFirst(REGIONS_IN_PARALLEL);
                _regionsSinceIdle += regions.Length;

                ChunkEnumerator.Enumerate(regions, EnumerationBody);
                TriggerInvoke();
            }

            TriggerInvoke();

            if (_regionsSinceIdle > 0)
            {
                if (_regionsSinceIdle >= COLLECT_AFTER_REGIONS)
                {
                    _collectDeadline = System.Diagnostics.Stopwatch.GetTimestamp() + CollectDelayTicks;
                }

                _regionsSinceIdle = 0;
            }
            else if (_collectDeadline != 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= _collectDeadline)
            {
                _collectDeadline = 0;
                GC.Collect(2, GCCollectionMode.Aggressive);
            }
        }

        private void TriggerInvoke()
        {
            Action? invoke = _invoke;
            if (invoke is null) return;

            invoke();
            lock (_invokeLock)
            {
                if (_newInvoke) _newInvoke = false;
                else _invoke = null;
            }
        }

        public void Invoke(Action action)
        {
            lock (_invokeLock)
            {
                _invoke = action;
                _newInvoke = true;
            }
        }
    }
}
