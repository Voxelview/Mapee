using CommonUtilities.Data;
using Mapper;
using Mapper.Gui.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using WorldEditor;

namespace Mapee.Benchmark
{
    /// <summary>
    /// Headless benchmark: loads every region of a world through the exact same
    /// WorldMapper pipeline the GUI uses (read -> decompress -> NBT -> scan -> render
    /// -> WriteableBitmap), and reports wall time.
    ///
    /// Usage: Mapee.Benchmark [worldDir] [resourcesRoot] [maxRegions]
    ///   worldDir      defaults to C:\Users\tawle\Desktop\PreGen_8192r
    ///   resourcesRoot defaults to the repo root (folder containing Resources\)
    ///   maxRegions    defaults to all
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string worldDir = args.Length > 0 ? args[0] : @"C:\Users\tawle\Desktop\PreGen_8192r";
            string resourcesRoot = args.Length > 1 ? args[1] : FindResourcesRoot();
            int maxRegions = args.Length > 2 ? int.Parse(args[2]) : int.MaxValue;

            Environment.CurrentDirectory = resourcesRoot;

            Level? level = new LevelReader().Read(Path.Combine(worldDir, "level.dat"));
            if (level is null)
            {
                Console.Error.WriteLine($"Could not read level.dat in {worldDir}");
                return 1;
            }
            Console.WriteLine($"World: {worldDir}  (version {level.Version.Version})");

            AssetPack assetPack = new AssetPackFactory().Create(new DataReader(@"Resources\Mapper\DefaultAsset"));
            ChunkMapperPack mapperPack = new(assetPack);

            // Same profile push the GUI does on world load: default style, current dimension.
            Style style = LoadDefaultStyle(assetPack);
            mapperPack.UpdatePack(style.GetProfile(Dimension.Overworld), style.ScanType);

            WorldMapper mapper = new(mapperPack);
            SceneInfo scene = new(level, Dimension.Overworld);

            Coords[] regions = scene.RegionStore.Itemize(
                $"{level.Directory}\\{Dimension.Overworld.GetRegionFolder(level.Version.Version)}").ToArray();
            if (regions.Length > maxRegions) regions = regions[..maxRegions];
            Console.WriteLine($"Regions: {regions.Length}");

            int rendered = 0, bitmaps = 0;
            long firstRegionTicks = 0;
            ManualResetEventSlim done = new(false);
            Stopwatch stopwatch = new();

            // The GUI keeps every region's bitmap alive as the visible map; retain them here
            // too so working-set numbers match what task manager shows for the real app.
            System.Collections.Concurrent.ConcurrentBag<System.Windows.Media.ImageSource> retainedBitmaps = new();

            mapper.RegionRendered = (coords, canvas) =>
            {
                // The GUI turns every canvas into a frozen WriteableBitmap; include that cost.
                if (canvas.GetBitmap() is System.Windows.Media.ImageSource bitmap)
                {
                    retainedBitmaps.Add(bitmap);
                    Interlocked.Increment(ref bitmaps);
                }

                int nowRendered = Interlocked.Increment(ref rendered);
                if (nowRendered == 1) Interlocked.Exchange(ref firstRegionTicks, stopwatch.ElapsedMilliseconds);
                if (nowRendered == regions.Length) done.Set();
            };

            mapper.SetScene(scene);

            TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes();

            // UI-thread stand-in: a Normal-priority thread that wants the CPU for a moment
            // every 10 ms, like input/dispatcher processing. Records how late it runs.
            long heartbeatMax = 0, heartbeatOver50 = 0, heartbeatTicks = 0;
            bool heartbeatStop = false;
            Thread heartbeat = new(() =>
            {
                while (!Volatile.Read(ref heartbeatStop))
                {
                    long before = Stopwatch.GetTimestamp();
                    Thread.Sleep(10);
                    long delayMs = (Stopwatch.GetTimestamp() - before) * 1000 / Stopwatch.Frequency - 10;

                    heartbeatTicks++;
                    if (delayMs > heartbeatMax) heartbeatMax = delayMs;
                    if (delayMs > 50) heartbeatOver50++;
                }
            })
            { IsBackground = true, Priority = ThreadPriority.Normal };
            heartbeat.Start();

            stopwatch.Start();
            mapper.Queue.ReplaceWith(regions.AsMemory());

            if (!done.Wait(TimeSpan.FromMinutes(10)))
            {
                Console.Error.WriteLine($"TIMEOUT - rendered {rendered}/{regions.Length}");
                return 2;
            }
            stopwatch.Stop();
            Volatile.Write(ref heartbeatStop, true);

            double seconds = stopwatch.Elapsed.TotalSeconds;
            TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
            double utilization = cpu.TotalSeconds / (seconds * Environment.ProcessorCount) * 100;

            Console.WriteLine($"RESULT: {regions.Length} regions in {seconds:F2}s  ({regions.Length / seconds:F1} regions/s, {bitmaps} bitmaps, first region at {firstRegionTicks} ms)");
            Console.WriteLine($"CPU: {cpu.TotalSeconds:F1}s over {Environment.ProcessorCount} LPs = {utilization:F0}% utilization");
            Console.WriteLine($"GC: gen0={GC.CollectionCount(0) - gen0} gen1={GC.CollectionCount(1) - gen1} gen2={GC.CollectionCount(2) - gen2} allocated={(GC.GetTotalAllocatedBytes() - allocated) / (1024.0 * 1024 * 1024):F2} GB");
            Console.WriteLine($"UI-PROXY: worst-delay={heartbeatMax} ms, ticks-delayed-over-50ms={heartbeatOver50}/{heartbeatTicks}");

            Process process = Process.GetCurrentProcess();
            process.Refresh();
            Console.WriteLine($"MEMORY: peak-workingset={process.PeakWorkingSet64 / (1024.0 * 1024):F0} MB workingset-at-end={process.WorkingSet64 / (1024.0 * 1024):F0} MB gc-heap={GC.GetTotalMemory(false) / (1024.0 * 1024):F0} MB");

            // WorldMapper schedules its own aggressive collection half a second after a large
            // load goes idle; wait it out and report what task manager shows from then on.
            Thread.Sleep(2000);
            process.Refresh();
            Console.WriteLine($"MEMORY-SETTLED: workingset={process.WorkingSet64 / (1024.0 * 1024):F0} MB gc-heap={GC.GetTotalMemory(false) / (1024.0 * 1024):F0} MB");
            GC.KeepAlive(retainedBitmaps);

            double ticksToS = 1.0 / Stopwatch.Frequency;
            double batchSlotS = ChunkEnumerator.BatchSlotTicks * ticksToS;
            double regionBusyS = ChunkEnumerator.RegionBusyTicks * ticksToS;
            Console.WriteLine($"BATCH: slot-time={batchSlotS:F1}s busy={regionBusyS:F1}s straggler-idle={batchSlotS - regionBusyS:F1}s ({(1 - regionBusyS / batchSlotS) * 100:F0}% of batch slot-time)");
            Console.WriteLine($"RENDER: total={Mapper.WorldMapperEnumerationBody.RenderTicks * ticksToS:F1}s enumerator-stalled-on-render={Mapper.WorldMapperEnumerationBody.RenderWaitTicks * ticksToS:F1}s cycles={Mapper.WorldMapperEnumerationBody.CycleCount}");
            Console.WriteLine($"NBT: skipped-subtrees={NbtEditor.NbtTagSkipper.SkipCount}");
            Console.WriteLine($"STAGES: decompress={ChunkEnumeratorFromRegion.DecompressTicks * ticksToS:F1}s nbt={ChunkEnumeratorFromRegion.DeserializeTicks * ticksToS:F1}s chunkread={ChunkEnumeratorFromRegion.ChunkReadTicks * ticksToS:F1}s " +
                $"convert={Mapper.WorldMapperEnumerationBody.ConvertTicks * ticksToS:F1}s scan={Mapper.WorldMapperEnumerationBody.ScanTicks * ticksToS:F1}s step={Mapper.WorldMapperEnumerationBody.StepTicks * ticksToS:F1}s");
            return 0;
        }

        private static Style LoadDefaultStyle(AssetPack defaultAssetPack)
        {
            List<Style> styles = new();
            HashSet<string> entries = new();
            entries.UnionWith(Directory.GetFiles(@"Resources\Mapper.Gui\Styles"));
            entries.UnionWith(Directory.GetDirectories(@"Resources\Mapper.Gui\Styles"));

            StyleReader styleReader = new(defaultAssetPack);
            foreach (string entry in entries)
            {
                Style? read = styleReader.Read(new DataReader(entry));
                if (read is not null) styles.Add(read);
            }

            return styles.OrderBy(x => x.Metadata?.OrderedIndex).First();
        }

        private static string FindResourcesRoot()
        {
            // Walk up from the exe until a Resources folder with the default asset shows up.
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Resources", "Mapper", "DefaultAsset"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("No Resources\\Mapper\\DefaultAsset found above the exe.");
        }
    }
}
