namespace WorldEditor
{
    public class ApiChunkVersionConverter : IVersionConverter
    {
        public IVersionConverter BlockStateVersionConverter { get; set; }
        public IVersionConverter BiomeVersionConverter { get; set; }
        public IVersionConverter BlockLightVersionConverter { get; set; } = new LightVersionConverter();
        public IVersionConverter SkyLightVersionConverter { get; set; } = new LightVersionConverter();
        public IVersionConverter HeightmapVersionConverter { get; set; } = new HeightmapVersionConverter();

        /// <param name="translatorsDirectory">The Translators folder, with Block\ and Biome\ inside.</param>
        public ApiChunkVersionConverter(string translatorsDirectory)
        {
            BlockStateVersionConverter = new BlockStateVersionConverter(Path.Combine(translatorsDirectory, "Block"));
            BiomeVersionConverter = new BiomeVersionConverter(Path.Combine(translatorsDirectory, "Biome"));
        }

        public IObject? Convert(IObject input, Version from, Version to, UsageIntent intent)
        {
            if (input is not ApiChunk chunk) return null;

            ConvertedApiChunk output = new(chunk);

            if (chunk.BlockState is not null)
            {
                IObject? blockState = BlockStateVersionConverter.Convert(chunk.BlockState, from, to, intent);
                if (blockState is BlockStateChunk converted) 
                {
                    output.BlockState = converted;
                }
            }

            if (chunk.Biome is not null)
            {
                IObject? biome = BiomeVersionConverter.Convert(chunk.Biome, from, to, intent);
                if (biome is BiomeChunk converted)
                {
                    output.Biome = converted;
                }
            }

            if (chunk.BlockLight is not null)
            {
                IObject? blockLight = BlockLightVersionConverter.Convert(chunk.BlockLight, from, to, intent);
                if (blockLight is LightChunk converted)
                {
                    output.BlockLight = converted;
                }
            }

            if (chunk.SkyLight is not null)
            {
                IObject? skyLight = SkyLightVersionConverter.Convert(chunk.SkyLight, from, to, intent);
                if (skyLight is LightChunk converted)
                {
                    output.SkyLight = converted;
                }
            }

            if (chunk.Heightmap is not null)
            {
                IObject? heightmap = HeightmapVersionConverter.Convert(chunk.Heightmap, from, to, intent);
                if (heightmap is HeightmapCollection converted)
                {
                    output.Heightmap = converted;
                }
            }

            return output;
        }
    }
}
