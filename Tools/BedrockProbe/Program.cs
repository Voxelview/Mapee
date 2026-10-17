using System.Collections.Concurrent;
using WorldEditor;

// Loads a Bedrock world through the same path Mapee uses (level reader, region store, chunk enumerator,
// version converter) and reports what came out. Run from the repository root so Resources/ resolves.
//   BedrockProbe <world folder> [overworld.png] [nether.png]
string world = args.Length > 0 ? args[0] : throw new ArgumentException("Usage: BedrockProbe <world folder> [overworld.png] [nether.png]");

Level level = new AnyEditionLevelReader().Read(Path.Combine(world, "level.dat")) ?? throw new InvalidDataException("No level.dat");
Console.WriteLine($"{level.WorldName}: {level.Edition}, {level.Version.VersionName}, {level.GameType}, last played {level.LastPlayed:u}");
Console.WriteLine($"player at {level.Player.Position} in {level.Player.Dimension.Name}, world spawn {level.WorldGen.WorldSpawn}, seed {level.WorldGen.Seed}");

string[] outputs = [.. args.Skip(1)];
Dimension[] dimensions = [Dimension.Overworld, Dimension.Nether, Dimension.TheEnd];
for (int d = 0; d < dimensions.Length; d++)
{
    Dimension dimension = dimensions[d];
    IRegionStore store = RegionStoreFactory.CreateRegionStore(level, dimension);

    var watch = System.Diagnostics.Stopwatch.StartNew();
    Coords[] regions = [.. store.Itemize(level.Directory!)];
    long indexTime = watch.ElapsedMilliseconds;
    if (regions.Length == 0)
    {
        Console.WriteLine($"{dimension.Name}: no chunks");
        continue;
    }

    CollectingBody body = new();
    ChunkEnumerator enumerator = new(4, 8, () => store, new ChunkEnumeratorFromRegionFactory(i => new ApiChunkReader()));
    watch.Restart();
    enumerator.Enumerate(regions, body);

    Console.WriteLine($"{dimension.Name}: {regions.Length} regions indexed in {indexTime} ms, {body.Chunks.Count} chunks loaded in {watch.ElapsedMilliseconds} ms");
    body.Report();

    if (d < outputs.Length) SurfaceRender.Render(body.Chunks.Values, dimension == Dimension.Nether ? (short)100 : (short)400, Path.Combine("Resources", "Mapper", "DefaultAsset", "BlockColor"), outputs[d]);
    (store as IDisposable)?.Dispose();
}

class CollectingBody : IEnumerationBody
{
    public ConcurrentDictionary<Coords, ConvertedApiChunk> Chunks { get; } = new();

    private readonly ApiChunkVersionConverter _converter = new();
    private int _withBiomes, _withHeightmap, _withSkyLight, _sections;

    public void BeginCycle() { }
    public void EndCycle() { }
    public void BeginReadingRegion(int index, Coords region) { }
    public void EndReadingRegion(int index, Coords region) { }

    public void EndReadingChunk(int regionIndex, int chunkIndex, IChunk chunk)
    {
        if (chunk is not ApiChunk api) return;
        if (_converter.Convert(api, api.Version, WorldEditor.Version.Newest, UsageIntent.Read) is not ConvertedApiChunk converted) return;

        Chunks[new Coords(chunk.X, chunk.Z)] = converted;
        if (converted.Biome?.Sections.Count > 0) Interlocked.Increment(ref _withBiomes);
        if (converted.Heightmap?.Heightmaps.ContainsKey("WORLD_SURFACE") == true) Interlocked.Increment(ref _withHeightmap);
        if (converted.SkyLight?.Sections.Count > 0) Interlocked.Increment(ref _withSkyLight);
        Interlocked.Add(ref _sections, converted.BlockState?.Sections.Count ?? 0);
    }

    public void Report()
    {
        Console.WriteLine($"  after conversion: {_sections} block sections, biomes in {_withBiomes}, heightmaps in {_withHeightmap}, sky light in {_withSkyLight}");
    }
}
