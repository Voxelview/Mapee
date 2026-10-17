namespace WorldEditor
{
    // Bedrock doesn't save light, but Mapee shades every block below the surface by sky light,
    // so without it water and anything under glass would render almost black. This approximates
    // Java's sky light by passing light straight down each column: clear blocks keep it, water and
    // leaves dim it by one per block, and anything else stops it. Java also spreads light sideways,
    // which is skipped, so caves near openings come out darker than in Java
    public static class BedrockSkyLight
    {
        public static LightChunk Compute(BlockStateChunk blockState, int dimension)
        {
            LightChunk output = new();
            if (blockState.Sections.Count < 1) return output;

            Dictionary<sbyte, PaletteSection<Block>> sections = blockState.Sections.ToDictionary(x => x.Y);
            sbyte top = blockState.Sections.Max(x => x.Y);
            sbyte bottom = BedrockDimension.LowestSection(dimension);

            output.Sections.Add(Uniform((sbyte)(top + 1), 15));

            byte[] column = new byte[256];
            Array.Fill(column, (byte)15);
            short[] indexes = new short[4096];

            for (sbyte y = top; y >= bottom; y--)
            {
                if (!sections.TryGetValue(y, out PaletteSection<Block>? section))
                {
                    output.Sections.Add(FromColumns(y, column));
                    continue;
                }

                byte[] opacity = section.Palette.Select(Opacity).ToArray();
                if (section.Type == SectionType.Uniform) Array.Clear(indexes);
                else section.Unlock(indexes);

                LightChunk.Section light = new(new byte[2048]) { Y = y };
                bool anyLight = false;
                for (int by = 15; by >= 0; by--)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        int index = by << 8 | i;
                        int value = Math.Max(0, column[i] - opacity[indexes[index]]);
                        column[i] = (byte)value;

                        if (value > 0)
                        {
                            light.SetLight(index, (byte)value);
                            anyLight = true;
                        }
                    }
                }

                output.Sections.Add(light);
                if (!anyLight) break;
            }

            return output;
        }

        private static LightChunk.Section Uniform(sbyte y, byte value)
        {
            byte[] values = new byte[2048];
            Array.Fill(values, (byte)(value | value << 4));
            return new LightChunk.Section(values) { Y = y };
        }
        private static LightChunk.Section FromColumns(sbyte y, byte[] column)
        {
            LightChunk.Section output = new(new byte[2048]) { Y = y };
            for (int by = 0; by < 16; by++)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (column[i] > 0) output.SetLight(by << 8 | i, column[i]);
                }
            }
            return output;
        }

        private static readonly string[] Dimming = ["water", "leaves", "kelp", "seagrass", "bubble_column", "ice", "cobweb", "slime_block", "honey_block"];
        private static readonly string[] Clear =
        [
            "air", "glass", "_pane", "torch", "lantern", "flower", "grass", "fern", "sapling", "bush", "vine", "lichen",
            "rail", "button", "lever", "pressure_plate", "sign", "banner", "carpet", "snow", "lily_pad", "mushroom",
            "sugar_cane", "bamboo", "wheat", "carrots", "potatoes", "beetroots", "stem", "berry", "tulip", "orchid",
            "allium", "bluet", "daisy", "dandelion", "poppy", "cornflower", "lily", "rose", "peony", "lilac", "sunflower",
            "roots", "sprouts", "fungus", "coral", "pickle", "candle", "redstone_wire", "repeater", "comparator", "tripwire",
            "ladder", "chain", "bars", "fence", "wall", "door", "spore", "dripleaf", "azalea", "petals", "moss_carpet",
            "light", "barrier", "structure_void", "scaffolding", "end_rod", "string", "frogspawn", "hanging_roots", "head", "skull"
        ];

        // Solid blocks whose names contain one of the clear words, like grass_block or crimson_stem
        private static readonly string[] Solid = ["_block", "_planks", "_stem", "_hyphae", "_wool", "_concrete", "_terracotta", "sea_lantern", "jack_o_lantern", "minecraft:light_"];

        private static byte Opacity(Block block)
        {
            string name = block.Name;
            foreach (string part in Dimming) if (name.Contains(part)) return 1;
            foreach (string part in Solid) if (name.Contains(part)) return 15;
            foreach (string part in Clear) if (name.Contains(part)) return 0;
            return 15;
        }
    }
}
