using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;
using WorldEditor;

// A rough top-down render from the converted chunks Mapee's renderer would receive. It isn't Mapee's
// renderer, just enough to check blocks, heightmaps, sky light and biomes line up. Water is drawn over
// the block beneath it, shaded by that block's sky light. Names with no colour are magenta and counted.
// A second image, <name>-biomes.png, colours each column by its biome
static partial class SurfaceRender
{
    public static void Render(ICollection<ConvertedApiChunk> chunks, short startY, string colorDirectory, string output)
    {
        Dictionary<string, uint> colors = LoadColors(colorDirectory);

        int minX = chunks.Min(c => c.X), maxX = chunks.Max(c => c.X);
        int minZ = chunks.Min(c => c.Z), maxZ = chunks.Max(c => c.Z);
        int width = (maxX - minX + 1) * 16, height = (maxZ - minZ + 1) * 16;
        uint[] pixels = new uint[width * height], biomePixels = new uint[width * height];

        Dictionary<string, int> missing = [], biomes = [];
        int columns = 0;

        foreach (ConvertedApiChunk chunk in chunks)
        {
            if (chunk.BlockState is null) continue;

            Dictionary<int, (PaletteSection<Block> Section, short[] Indexes)> sections = [];
            foreach (var section in chunk.BlockState.Sections)
            {
                short[] indexes = new short[4096];
                if (section.Type == SectionType.Normal) section.Unlock(indexes);
                sections[section.Y] = (section, indexes);
            }
            Dictionary<int, LightChunk.Section> light = chunk.SkyLight?.Sections.ToDictionary(x => (int)x.Y) ?? [];
            Dictionary<int, (PaletteSection<string> Section, short[] Indexes)> biomeSections = [];
            foreach (var section in chunk.Biome?.Sections ?? [])
            {
                short[] indexes = new short[64];
                if (section.Type == SectionType.Normal) section.Unlock(indexes);
                biomeSections[section.Y] = (section, indexes);
            }

            short[] heights = new short[256];
            bool hasHeightmap = startY > 300 && chunk.Heightmap is not null && chunk.Heightmap.Heightmaps.TryGetValue("WORLD_SURFACE", out var heightmap);
            if (hasHeightmap) chunk.Heightmap!.Heightmaps["WORLD_SURFACE"].Unlock(heights);

            int lowest = sections.Count > 0 ? sections.Keys.Min() * 16 : 0;
            for (int column = 0; column < 256; column++)
            {
                int x = column % 16, z = column / 16;
                int y = hasHeightmap ? heights[column] - 1 : startY;

                // Below a roof, as in the Nether, skip down to the first air before looking for ground
                bool seenAir = hasHeightmap;
                string? surface = null, ground = null;
                int surfaceY = 0, groundY = 0;
                for (; y >= lowest; y--)
                {
                    string name = BlockAt(sections, x, y, z);
                    if (name.EndsWith("air")) { seenAir = true; continue; }
                    if (!seenAir) continue;

                    surface ??= IsWater(name) ? "minecraft:water" : name;
                    if (surface == "minecraft:water" && IsWater(name)) { surfaceY = surfaceY == 0 ? y : surfaceY; continue; }
                    ground = name;
                    groundY = y;
                    break;
                }
                if (ground is null) continue;
                columns++;

                uint color = ColorOf(colors, ground, missing);
                float shade = Math.Clamp(0.55f + (groundY - 62) / 160f, 0.3f, 1.2f);
                if (surface == "minecraft:water")
                {
                    byte skyLight = LightAt(light, x, groundY + 1, z);
                    color = Blend(Scale(color, 0.25f + 0.75f * skyLight / 15f), colors["minecraft:water"], 0.55f);
                    shade = 1;
                }

                int px = (chunk.X - minX) * 16 + x, pz = (chunk.Z - minZ) * 16 + z;
                pixels[pz * width + px] = Scale(color, shade);

                string biome = BiomeAt(biomeSections, x, groundY, z);
                biomes[biome] = biomes.GetValueOrDefault(biome) + 1;
                biomePixels[pz * width + px] = 0xFF000000 | (uint)biome.GetHashCode() & 0xFFFFFF;
            }
        }

        WritePng(output, width, height, pixels);
        WritePng(Path.ChangeExtension(output, null) + "-biomes.png", width, height, biomePixels);

        int missed = missing.Values.Sum();
        Console.WriteLine($"  rendered {width}x{height} to {output}; {missed} of {columns} columns ({100.0 * missed / Math.Max(1, columns):F2}%) had no colour");
        foreach (var name in missing.OrderByDescending(x => x.Value).Take(15)) Console.WriteLine($"    {name.Key}: {name.Value}");
        Console.WriteLine("  biomes: " + string.Join(", ", biomes.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key.Replace("minecraft:", "")} {x.Value}")));
    }

    private static bool IsWater(string name) => name is "minecraft:water" or "minecraft:seagrass" or "minecraft:tall_seagrass" or "minecraft:kelp" or "minecraft:kelp_plant" or "minecraft:bubble_column";

    private static string BlockAt(Dictionary<int, (PaletteSection<Block> Section, short[] Indexes)> sections, int x, int y, int z)
    {
        if (!sections.TryGetValue(y >> 4, out var section)) return "minecraft:air";
        int index = section.Section.Type == SectionType.Uniform ? 0 : section.Indexes[(y & 15) << 8 | z << 4 | x];
        return section.Section.Palette[index].Name;
    }
    private static byte LightAt(Dictionary<int, LightChunk.Section> light, int x, int y, int z)
    {
        return light.TryGetValue(y >> 4, out var section) ? section.GetLight((y & 15) << 8 | z << 4 | x) : (byte)0;
    }
    private static string BiomeAt(Dictionary<int, (PaletteSection<string> Section, short[] Indexes)> sections, int x, int y, int z)
    {
        if (!sections.TryGetValue(y >> 4, out var section)) return "none";
        int index = section.Section.Type == SectionType.Uniform ? 0 : section.Indexes[MathUtilities.FindBiomeIndex(x, y, z)];
        return section.Section.Palette[index];
    }

    private static uint ColorOf(Dictionary<string, uint> colors, string name, Dictionary<string, int> missing)
    {
        if (colors.TryGetValue(name, out uint color)) return color;
        missing[name] = missing.GetValueOrDefault(name) + 1;
        return 0xFFFF00FF;
    }
    private static uint Scale(uint color, float factor)
    {
        uint r = (uint)Math.Clamp(((color >> 16) & 0xFF) * factor, 0, 255);
        uint g = (uint)Math.Clamp(((color >> 8) & 0xFF) * factor, 0, 255);
        uint b = (uint)Math.Clamp((color & 0xFF) * factor, 0, 255);
        return 0xFF000000 | r << 16 | g << 8 | b;
    }
    private static uint Blend(uint under, uint over, float amount)
    {
        uint Mix(int shift) => (uint)(((under >> shift) & 0xFF) * (1 - amount) + ((over >> shift) & 0xFF) * amount);
        return 0xFF000000 | Mix(16) << 16 | Mix(8) << 8 | Mix(0);
    }

    // Biome-tinted blocks are grey in the colour table; give the common ones a plain tint so the image reads
    private static readonly Dictionary<string, uint> Tinted = new()
    {
        ["minecraft:grass_block"] = 0xFF7CBD6B, ["minecraft:short_grass"] = 0xFF7CBD6B, ["minecraft:tall_grass"] = 0xFF7CBD6B,
        ["minecraft:fern"] = 0xFF6AA85A, ["minecraft:large_fern"] = 0xFF6AA85A, ["minecraft:water"] = 0xFF3F76E4, ["minecraft:vine"] = 0xFF59AE30
    };

    private static Dictionary<string, uint> LoadColors(string directory)
    {
        Dictionary<string, uint> output = [];
        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            // Entries with property conditions span lines; take the first colour after each name
            string text = File.ReadAllText(file);
            foreach (Match entry in EntryPattern().Matches(text))
            {
                output.TryAdd(entry.Groups[1].Value, Convert.ToUInt32(entry.Groups[2].Value, 16) | 0xFF000000);
            }
        }

        foreach (var tinted in Tinted) output[tinted.Key] = tinted.Value;
        foreach (string name in output.Keys.Where(n => n.EndsWith("leaves")).ToList()) output[name] = 0xFF48B518;
        return output;
    }

    [GeneratedRegex("\"(minecraft:[a-z0-9_]+)\\*?\"[^#]*?\"#([0-9A-Fa-f]{8})\"", RegexOptions.Singleline)]
    private static partial Regex EntryPattern();

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
