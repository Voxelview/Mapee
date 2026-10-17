namespace WorldEditor
{
    // Builds the same ApiChunk a Java chunk produces, from one chunk's Bedrock records.
    // Bedrock doesn't save light, so sky light is approximated and block light stays empty
    public class BedrockChunkReader
    {
        public BedrockSubChunkReader SubChunkReader { get; set; } = new();
        public BedrockData3DReader Data3DReader { get; set; } = new();
        public BedrockBlockTranslator BlockTranslator { get; set; } = BedrockBlockTranslator.Default;

        public bool RemoveEmptyAirSections { get; set; } = true;
        public bool ComputeSkyLight { get; set; } = true;

        public ApiChunk? Read(int x, int z, int dimension, IDictionary<byte[], byte[]> records)
        {
            BlockStateChunk blockState = new();
            byte[]? data3D = null;

            foreach (var record in records)
            {
                if (!BedrockKey.TryParse(record.Key, out BedrockKey key) || key.Dimension != dimension) continue;
                if (key.X != x || key.Z != z) continue;

                if (key.Tag == BedrockChunkTag.Data3D)
                {
                    data3D = record.Value;
                }
                else if (key.Tag == BedrockChunkTag.SubChunkPrefix)
                {
                    PaletteSection<Block>? section = ReadSection(record.Value, key.SubChunkY);
                    if (section is not null) blockState.Sections.Add(section);
                }
            }

            if (blockState.Sections.Count < 1) return null;

            ApiChunk output = new()
            {
                X = x,
                Z = z,
                Version = Version.Newest,
                BlockState = blockState,
                SkyLight = ComputeSkyLight ? BedrockSkyLight.Compute(blockState, dimension) : null
            };

            if (data3D is not null && data3D.Length >= 512)
            {
                output.Heightmap = Data3DReader.ReadHeightmap(data3D, dimension);
                output.Biome = Data3DReader.ReadBiomes(data3D, dimension);
            }

            return output;
        }

        // Layer 0 holds the blocks; layer 1 is the water a block is logged in, which Java draws as the block itself
        protected virtual PaletteSection<Block>? ReadSection(byte[] data, sbyte keyY)
        {
            BedrockSubChunkReader.SubChunk? subChunk = SubChunkReader.Read(data, keyY);
            if (subChunk is null || subChunk.Layers.Count < 1) return null;

            PaletteSection<Block> section = subChunk.Layers[0];
            BlockTranslator.Translate(section.Palette);

            if (RemoveEmptyAirSections && section.Palette.Length == 1 && section.Palette[0].Name == "minecraft:air") return null;
            return section;
        }
    }
}
