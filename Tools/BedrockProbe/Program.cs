using WorldEditor;

string world = args.Length > 0 ? args[0] : throw new ArgumentException("Usage: BedrockProbe <world folder>");

using LevelDbReader db = new(Path.Combine(world, "db"));

Dictionary<int, HashSet<(int, int)>> chunks = [];
List<BedrockKey> subChunkKeys = [];
db.ForEachKey(key =>
{
    if (!BedrockKey.TryParse(key, out BedrockKey parsed)) return;
    if (parsed.Tag == BedrockChunkTag.Version || parsed.Tag == BedrockChunkTag.LegacyVersion)
    {
        if (!chunks.TryGetValue(parsed.Dimension, out var set)) chunks[parsed.Dimension] = set = [];
        set.Add((parsed.X, parsed.Z));
    }
});

foreach (var dimension in chunks.OrderBy(x => x.Key))
{
    var xs = dimension.Value.Select(c => c.Item1);
    var zs = dimension.Value.Select(c => c.Item2);
    Console.WriteLine($"dimension {dimension.Key}: {dimension.Value.Count} chunks, x {xs.Min()}..{xs.Max()}, z {zs.Min()}..{zs.Max()}");
}

BedrockSubChunkReader reader = new();
Dictionary<string, int> versions = [], bitCounts = [], layerCounts = [];
Dictionary<string, int> blockNames = [];
int failures = 0, sections = 0;
short[] unlocked = new short[4096];

foreach (var dimension in chunks)
{
    foreach (var (x, z) in dimension.Value)
    {
        foreach (var record in db.GetByPrefix(BedrockKey.ChunkPrefix(x, z, dimension.Key)))
        {
            if (!BedrockKey.TryParse(record.Key, out BedrockKey key) || key.Dimension != dimension.Key || key.Tag != BedrockChunkTag.SubChunkPrefix) continue;

            string version = record.Value.Length > 0 ? record.Value[0].ToString() : "empty";
            versions[version] = versions.GetValueOrDefault(version) + 1;

            try
            {
                var subChunk = reader.Read(record.Value, key.SubChunkY);
                if (subChunk is null) { failures++; continue; }
                sections++;

                layerCounts[subChunk.Layers.Count.ToString()] = layerCounts.GetValueOrDefault(subChunk.Layers.Count.ToString()) + 1;
                foreach (var layer in subChunk.Layers)
                {
                    string bits = layer.Locker!.BitCount.ToString();
                    bitCounts[bits] = bitCounts.GetValueOrDefault(bits) + 1;
                    layer.Unlock(unlocked);
                    foreach (Block block in layer.Palette) blockNames[block.Name] = blockNames.GetValueOrDefault(block.Name) + 1;
                }
            }
            catch (Exception e)
            {
                if (failures++ < 5) Console.WriteLine($"  failed {x},{z} dim {dimension.Key} y {key.SubChunkY}: {e.Message}");
            }
        }
    }
}

Console.WriteLine($"{sections} sub-chunks decoded, {failures} failed");
Console.WriteLine("format versions: " + string.Join(", ", versions.OrderBy(x => x.Key).Select(x => $"v{x.Key}={x.Value}")));
Console.WriteLine("layers: " + string.Join(", ", layerCounts.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")));
Console.WriteLine("bits per block: " + string.Join(", ", bitCounts.OrderBy(x => int.Parse(x.Key)).Select(x => $"{x.Key}={x.Value}")));
Console.WriteLine($"{blockNames.Count} distinct block names; most common:");
foreach (var name in blockNames.OrderByDescending(x => x.Value).Take(12)) Console.WriteLine($"  {name.Key}: {name.Value}");

// Show one column at the world spawn so the decoded order can be checked by eye
var spawn = db.GetByPrefix(BedrockKey.ChunkPrefix(-2, 2, 0));
Console.WriteLine("spawn chunk (-2, 2), column x=9 z=4, top 12 non-air blocks:");
var column = new List<(int Y, string Name)>();
foreach (var record in spawn)
{
    if (!BedrockKey.TryParse(record.Key, out BedrockKey key) || key.Dimension != 0 || key.Tag != BedrockChunkTag.SubChunkPrefix) continue;
    var subChunk = reader.Read(record.Value, key.SubChunkY);
    if (subChunk is null || subChunk.Layers.Count < 1) continue;

    var layer = subChunk.Layers[0];
    layer.Unlock(unlocked);
    for (int y = 0; y < 16; y++)
    {
        Block block = layer.Palette[unlocked[y * 256 + 4 * 16 + 9]];
        if (block.Name != "minecraft:air") column.Add((subChunk.Y * 16 + y, block.Name));
    }
}
foreach (var block in column.OrderByDescending(x => x.Y).Take(12)) Console.WriteLine($"  y {block.Y}: {block.Name}");

// With a second argument, also write a top-down render of the overworld there
if (args.Length > 1 && chunks.TryGetValue(0, out var overworld))
{
    string colors = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Resources", "Mapper", "DefaultAsset", "BlockColor");
    SurfaceRender.Render(db, 0, overworld, colors, args[1]);
}
