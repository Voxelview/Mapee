using System.Buffers.Binary;

namespace WorldEditor
{
    // Data3D holds a 16x16 heightmap of int16s (Z-major, measured from the dimension floor, pointing at the
    // first air block like Java's WORLD_SURFACE), then one paletted biome storage per section from the bottom up
    public class BedrockData3DReader
    {
        public BedrockBiomeTranslator Translator { get; set; } = BedrockBiomeTranslator.Default;

        public HeightmapCollection ReadHeightmap(byte[] data, int dimension)
        {
            short floorY = BedrockDimension.FloorY(dimension);

            short[] values = new short[256];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (short)(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2)) + floorY);
            }

            HeightmapCollection.Heightmap heightmap = new([]) { FloorY = floorY };
            heightmap.Locker!.Writer = new SimpleBlockStateWriter();
            heightmap.Lock(values);

            HeightmapCollection output = new();
            output.Heightmaps.Add("WORLD_SURFACE", heightmap);
            return output;
        }

        public BiomeChunk ReadBiomes(byte[] data, int dimension)
        {
            BiomeChunk output = new();
            sbyte y = BedrockDimension.LowestSection(dimension);

            PaletteSection<string>? previous = null;
            int position = 512;
            while (position < data.Length)
            {
                PaletteSection<string>? section = ReadSection(data, ref position, y, previous);
                if (section is not null) output.Sections.Add(section);

                previous = section;
                y++;
            }

            return output;
        }

        protected virtual PaletteSection<string>? ReadSection(byte[] data, ref int position, sbyte y, PaletteSection<string>? previous)
        {
            byte header = data[position++];

            // 0xFF repeats the section below
            if (header == 0xFF)
            {
                if (previous is null) return null;
                return new PaletteSection<string>(previous.Palette, previous.Indexes)
                {
                    Y = y,
                    Locker = previous.Locker
                };
            }

            int bitCount = header >> 1;
            uint[] words = [];
            int paletteLength = 1;

            if (bitCount > 0)
            {
                int perWord = 32 / bitCount;
                words = new uint[(4096 + perWord - 1) / perWord];
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                    position += 4;
                }

                paletteLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
                position += 4;
            }

            string[] palette = new string[paletteLength];
            for (int i = 0; i < paletteLength; i++)
            {
                palette[i] = Translator.Translate(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position)));
                position += 4;
            }

            if (paletteLength == 1)
            {
                return new PaletteSection<string>(palette, []) { Y = y, Locker = new BedrockBiomeLocker() };
            }

            // Java keeps one biome per 4x4x4 cell, so sample each cell's corner block
            long[] cells = new long[64];
            uint mask = (1u << bitCount) - 1;
            int blocksPerWord = 32 / bitCount;
            for (int cy = 0; cy < 4; cy++)
            {
                for (int cz = 0; cz < 4; cz++)
                {
                    for (int cx = 0; cx < 4; cx++)
                    {
                        int block = (cx * 4) << 8 | (cz * 4) << 4 | (cy * 4);
                        uint value = (words[block / blocksPerWord] >> (block % blocksPerWord * bitCount)) & mask;
                        cells[cy * 16 + cz * 4 + cx] = value < paletteLength ? value : 0;
                    }
                }
            }

            return new PaletteSection<string>(palette, cells) { Y = y, Locker = new BedrockBiomeLocker() };
        }
    }

    // Biome cells are stored one per long in Mapee's x + z*4 + y*16 order, so unlocking is a copy
    public class BedrockBiomeLocker : Locker
    {
        public override int BitCount => 16;
        public override int UnlockedArrayLength => 4 * 4 * 4;

        public override long[] Lock(short[] unlockedArray)
        {
            return unlockedArray.Select(x => (long)x).ToArray();
        }
        public override void Unlock(long[] lockedArray, short[] outputArray)
        {
            for (int i = 0; i < lockedArray.Length && i < outputArray.Length; i++)
            {
                outputArray[i] = (short)lockedArray[i];
            }
        }
    }
}
