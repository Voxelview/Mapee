using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;
using WorldEditor;

// A rough top-down render: the highest non-air block per column, coloured from Mapee's Java colour table.
// Names with no colour are drawn magenta and counted, which shows what still needs translating
static partial class SurfaceRender
{
    public static void Render(LevelDbReader db, int dimension, IEnumerable<(int X, int Z)> chunks, string colorDirectory, string output)
    {
        Dictionary<string, uint> colors = LoadColors(colorDirectory);
        (int X, int Z)[] list = [.. chunks];

        int minX = list.Min(c => c.X), maxX = list.Max(c => c.X);
        int minZ = list.Min(c => c.Z), maxZ = list.Max(c => c.Z);
        int width = (maxX - minX + 1) * 16, height = (maxZ - minZ + 1) * 16;
        uint[] pixels = new uint[width * height];

        BedrockSubChunkReader reader = new();
        Dictionary<string, int> missing = [];
        int columns = 0;
        short[] unlocked = new short[4096];

        foreach (var (cx, cz) in list)
        {
            List<BedrockSubChunkReader.SubChunk> subChunks = [];
            foreach (var record in db.GetByPrefix(BedrockKey.ChunkPrefix(cx, cz, dimension)))
            {
                if (!BedrockKey.TryParse(record.Key, out BedrockKey key) || key.Dimension != dimension || key.Tag != BedrockChunkTag.SubChunkPrefix) continue;
                var subChunk = reader.Read(record.Value, key.SubChunkY);
                if (subChunk is not null && subChunk.Layers.Count > 0) subChunks.Add(subChunk);
            }

            string?[] top = new string?[256];
            foreach (var subChunk in subChunks.OrderByDescending(s => s.Y))
            {
                var layer = subChunk.Layers[0];
                layer.Unlock(unlocked);
                for (int column = 0; column < 256; column++)
                {
                    if (top[column] is not null) continue;
                    for (int y = 15; y >= 0; y--)
                    {
                        string name = layer.Palette[unlocked[y * 256 + column]].Name;
                        if (name == "minecraft:air" || name.EndsWith("_air")) continue;
                        top[column] = name;
                        break;
                    }
                }
            }

            for (int column = 0; column < 256; column++)
            {
                string? name = top[column];
                if (name is null) continue;
                columns++;

                if (!colors.TryGetValue(name, out uint color))
                {
                    missing[name] = missing.GetValueOrDefault(name) + 1;
                    color = 0xFFFF00FF;
                }

                int px = (cx - minX) * 16 + column % 16, pz = (cz - minZ) * 16 + column / 16;
                pixels[pz * width + px] = color;
            }
        }

        WritePng(output, width, height, pixels);

        int missed = missing.Values.Sum();
        Console.WriteLine($"rendered {width}x{height} to {output}; {missed} of {columns} surface columns ({100.0 * missed / Math.Max(1, columns):F1}%) had no colour");
        foreach (var name in missing.OrderByDescending(x => x.Value).Take(25)) Console.WriteLine($"  {name.Key}: {name.Value}");
    }

    // Biome-tinted blocks are grey in the colour table; give the common ones a plain tint so the image reads
    private static readonly Dictionary<string, uint> Tinted = new()
    {
        ["minecraft:grass_block"] = 0xFF7CBD6B, ["minecraft:short_grass"] = 0xFF7CBD6B, ["minecraft:tall_grass"] = 0xFF7CBD6B,
        ["minecraft:water"] = 0xFF3F76E4, ["minecraft:flowing_water"] = 0xFF3F76E4, ["minecraft:vine"] = 0xFF59AE30
    };

    private static Dictionary<string, uint> LoadColors(string directory)
    {
        Dictionary<string, uint> output = [];
        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            foreach (string line in File.ReadLines(file))
            {
                Match name = NamePattern().Match(line), color = ColorPattern().Match(line);
                if (name.Success && color.Success) output.TryAdd(name.Groups[1].Value, Convert.ToUInt32(color.Groups[1].Value, 16));
            }
        }

        foreach (var tinted in Tinted) output[tinted.Key] = tinted.Value;
        foreach (string name in output.Keys.Where(n => n.EndsWith("leaves")).ToList()) output[name] = 0xFF48B518;
        return output;
    }

    [GeneratedRegex("\"(minecraft:[a-z0-9_]+)\"")]
    private static partial Regex NamePattern();
    [GeneratedRegex("\"#([0-9A-Fa-f]{8})\"")]
    private static partial Regex ColorPattern();

    private static void WritePng(string file, int width, int height, uint[] argb)
    {
        using MemoryStream raw = new();
        using (ZLibStream zlib = new(raw, CompressionLevel.Optimal, true))
        {
            byte[] row = new byte[1 + width * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint c = argb[y * width + x];
                    row[1 + x * 3] = (byte)(c >> 16);
                    row[2 + x * 3] = (byte)(c >> 8);
                    row[3 + x * 3] = (byte)c;
                }
                zlib.Write(row);
            }
        }

        using FileStream output = File.Create(file);
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", raw.ToArray());
        WriteChunk(output, "IEND", []);
    }
    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32(Crc32(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
        byte[] crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }
    private static uint Crc32(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }
}
