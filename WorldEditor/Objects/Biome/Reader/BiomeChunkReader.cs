using NbtEditor;

namespace WorldEditor
{
    public class BiomeChunkReader : IObjectReader<ObjectReadParamter, BiomeChunk?>
    {
        public VersionList<string[]> SectionVersions { get; set; }

        public BiomeChunkReader()
        {
            SectionVersions = ChunkUtilities.GetSectionVersions();
        }

        public BiomeChunk? Read(ObjectReadParamter input)
        {
            if (!SectionVersions.TryRetrieveValue(input.Version, out string[]? path) || path is null) return null;
            if (!input.Level.TryGetChild(out Tag sectionListTag, path)) return null;

            BiomeChunk output = new()
            {
                DataTag = input.KeepDataTag ? sectionListTag : null
            };

            IBlockStateReader reader = ChunkUtilities.GetBlockStateReader(input.Version);

            if (sectionListTag is not ListTag sectionList) return null;
            foreach (var sectionTag in sectionList)
            {
                if (sectionTag is not CompoundTag sectionCompound) continue;

                PaletteSection<string>? section = ReadSection(input, sectionCompound);
                if (section is not null)
                {
                    output.Sections.Add(section);

                    section.Locker = new BiomeLocker(section)
                    {
                        Reader = reader
                    };
                }
            }

            return output;
        }
        protected virtual PaletteSection<string>? ReadSection(ObjectReadParamter parameter, CompoundTag section)
        {
            if (!section.TryGetChild("biomes", "palette", out Tag paletteTag)) return null;
            if (paletteTag is not ListTag paletteList) return null;

            string[] palette = new string[paletteList.Count];
            for (int i = 0; i < paletteList.Count; i++)
            {
                palette[i] = paletteList[i];
            }

            long[] data;
            if (section.TryGetChild("biomes", "data", out Tag dataTag))
            {
                data = dataTag;
            }
            else
            {
                data = Array.Empty<long>();
            }

            return new PaletteSection<string>(palette, data)
            {
                Y = section["Y"] ?? 0,
                DataTag = parameter.KeepDataTag ? section : null
            };
        }
    }
}
