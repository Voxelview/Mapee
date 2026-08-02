using CommonUtilities.Pool;
using MapScanner;
using System.Collections.Concurrent;
using WorldEditor;

namespace Mapper
{
    public class WorldMapperEnumerationBody : IEnumerationBody
    {
        public virtual MapperPack MapperPack { get; }
        public virtual Action<Coords, ICanvas>? RegionRendered { get; set; }

        /// <summary>
        /// Lets step slabs of fully-surrounded regions be dropped during a load. A region
        /// whose neighbor does not exist on disk would otherwise wait for it forever.
        /// Null keeps every slab until the scene resets.
        /// </summary>
        public virtual Func<Coords, bool>? RegionExists { get; set; }

        public int RegionsPerTask { get; }
        public int TasksPerRegion { get; }

        private readonly ScannedRegion?[] _savedRegions;

        /// <summary>
        /// Regions scanned but not yet handed to a render. Filled by slot workers, drained
        /// into a render batch every <see cref="RegionsPerTask"/> completions under
        /// <see cref="_renderLock"/>.
        /// </summary>
        private readonly List<ScannedRegion> _pendingRender = new();

        /// <summary>
        /// One persistent step store instead of the old per-batch double buffer. Renders are
        /// chained one after another, so a batch always sees every step slab loaded so far -
        /// a superset of what the per-batch buffer offered - and slabs are evicted once a
        /// region and all its existing neighbors have been rendered.
        /// </summary>
        private readonly StepProvider _stepProvider = new();

        /// <summary>Rendered coordinates; only the sequential render chain touches this.</summary>
        private readonly HashSet<Coords> _renderedRegions = new();

        /// <summary>The tail of the render chain. Guarded by <see cref="_renderLock"/>.</summary>
        private Task? _renderTask;
        private readonly object _renderLock = new();

        public static long RenderWaitTicks, RenderTicks;
        public static int CycleCount;

        public WorldMapperEnumerationBody(MapperPack mapperPack, int regionsPerTask, int tasksPerRegion)
        {
            MapperPack = mapperPack;
            RegionsPerTask = regionsPerTask;
            TasksPerRegion = tasksPerRegion;

            _savedRegions = new ScannedRegion[RegionsPerTask];
        }

        public void ResetScene()
        {
            WaitForPendingRender();

            for (int i = 0; i < _savedRegions.Length; i++)
            {
                _savedRegions[i] = null;
            }

            lock (_renderLock)
            {
                _pendingRender.Clear();
            }

            _renderedRegions.Clear();
            _stepProvider.Clear();
        }

        public void BeginCycle() { }
        public void EndCycle()
        {
            CycleCount++;

            // Dispatch what this cycle scanned, then wait only for the chain up to BEFORE
            // that dispatch: the new batch draws while the enumerator reads the next cycle,
            // and at most one cycle's rendering is ever outstanding beyond the current one.
            Task? previousTail;
            lock (_renderLock)
            {
                previousTail = _renderTask;
                DispatchRenderLocked();
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            previousTail?.Wait();
            System.Threading.Interlocked.Add(ref RenderWaitTicks, System.Diagnostics.Stopwatch.GetTimestamp() - start);
        }

        private void WaitForPendingRender()
        {
            Task? renderTask;
            lock (_renderLock)
            {
                renderTask = _renderTask;
                _renderTask = null;
            }

            if (renderTask is null) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            renderTask.Wait();
            System.Threading.Interlocked.Add(ref RenderWaitTicks, System.Diagnostics.Stopwatch.GetTimestamp() - start);
        }

        public void BeginReadingRegion(int index, Coords regionCoords)
        {
            _savedRegions[index] = new ScannedRegion(regionCoords);
        }
        public void EndReadingRegion(int index, Coords regionCoords)
        {
            ScannedRegion? region = _savedRegions[index];
            if (region is null) return;

            _savedRegions[index] = null;

            lock (_renderLock)
            {
                _pendingRender.Add(region);
                if (_pendingRender.Count >= RegionsPerTask)
                {
                    DispatchRenderLocked();
                }
            }
        }

        private void DispatchRenderLocked()
        {
            if (_pendingRender.Count == 0) return;

            ScannedRegion[] batch = _pendingRender.ToArray();
            _pendingRender.Clear();

            _renderTask = _renderTask is null
                ? Task.Run(() => RenderAndRelease(batch))
                : _renderTask.ContinueWith(_ => RenderAndRelease(batch), TaskScheduler.Default);
        }

        public static long ConvertTicks, ScanTicks, StepTicks;

        public void EndReadingChunk(int regionIndex, int chunkIndex, IChunk chunk)
        {
            if (chunk is not ApiChunk apiChunk) return;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            ConvertedApiChunk? convertedApiChunk = MapperPack.VersionConverter?.Convert(apiChunk, apiChunk.Version, WorldEditor.Version.Newest, UsageIntent.Read) as ConvertedApiChunk;
            if (convertedApiChunk is null) return;

            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            ThreadSlot.Index = regionIndex * TasksPerRegion + chunkIndex;
            IScannedChunk? scannedChunk = MapperPack.ChunkScanner?.Scan(convertedApiChunk);

            convertedApiChunk.Dispose();
            if (scannedChunk is null) return;

            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            StepChunk? stepChunk = MapperPack.StepChunkScanner?.Scan(scannedChunk);
            if(stepChunk is null) return;

            _stepProvider.Add(chunk.X, chunk.Z, stepChunk);
            _savedRegions[regionIndex]?.Add(scannedChunk);
            long t3 = System.Diagnostics.Stopwatch.GetTimestamp();

            System.Threading.Interlocked.Add(ref ConvertTicks, t1 - t0);
            System.Threading.Interlocked.Add(ref ScanTicks, t2 - t1);
            System.Threading.Interlocked.Add(ref StepTicks, t3 - t2);
        }

        private void RenderAndRelease(ScannedRegion[] regions)
        {
            ThreadPriority priority = WorkerPriority.Lower();
            try
            {
                Render(regions, _stepProvider);

                for (int i = 0; i < regions.Length; i++)
                {
                    _renderedRegions.Add(regions[i].Coords);
                }
                for (int i = 0; i < regions.Length; i++)
                {
                    ReleaseAround(regions[i].Coords);
                }
            }
            finally
            {
                WorkerPriority.Restore(priority);
            }
        }

        /// <summary>
        /// Renders only ever query the slabs of their own regions and those regions'
        /// neighbors, and the chain is sequential, so once a region and all its on-disk
        /// neighbors are rendered nothing can query its slab again.
        /// </summary>
        private void ReleaseAround(Coords rendered)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Coords candidate = new(rendered.X + dx, rendered.Z + dz);
                    if (!_renderedRegions.Contains(candidate)) continue;
                    if (!AllNeighborsSettled(candidate)) continue;

                    _stepProvider.RemoveRegion(candidate);
                }
            }
        }
        private bool AllNeighborsSettled(Coords coords)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    Coords neighbor = new(coords.X + dx, coords.Z + dz);
                    if (_renderedRegions.Contains(neighbor)) continue;
                    if (RegionExists is not null && !RegionExists(neighbor)) continue;

                    return false;
                }
            }

            return true;
        }

        protected virtual void Render(ScannedRegion[] regions, IStepProvider stepProvider)
        {
            if (MapperPack.MapRenderer is null) return;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            Parallel.For(0, regions.Length,
                WorkerPriority.Lower,
                (i, _, priority) =>
                {
                    MapperPack.MapRenderer.Render(new MapRenderArgs(regions[i].Chunks, stepProvider), out ICanvas canvas);
                    try
                    {
                        // The handler copies out of the canvas into a bitmap; it does not keep it.
                        RegionRendered?.Invoke(regions[i].Coords, canvas);
                    }
                    finally
                    {
                        canvas.Dispose();
                    }

                    return priority;
                },
                WorkerPriority.Restore);
            System.Threading.Interlocked.Add(ref RenderTicks, System.Diagnostics.Stopwatch.GetTimestamp() - start);
        }
    }
}
