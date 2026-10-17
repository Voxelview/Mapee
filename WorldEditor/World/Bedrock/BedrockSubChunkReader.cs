using NbtEditor;
using System.Buffers.Binary;

namespace WorldEditor
{
    // Reads SubChunkPrefix records, versions 1, 8 and 9. Older formats store numeric block IDs and aren't supported
    public class BedrockSubChunkReader
    {
        public class SubChunk
        {
            public sbyte Y { get; init; }
            public IList<PaletteSection<Block>> Layers { get; init; } = [];
        }

        public SubChunk? Read(byte[] data, sbyte keyY)
        {
            if (data.Length < 1) return null;

            int position = 0;
            byte version = data[position++];

            int layerCount;
            sbyte y = keyY;
            switch (version)
            {
                case 1:
                    layerCount = 1;
                    break;
                case 8:
                    layerCount = data[position++];
                    break;
                case 9:
                    layerCount = data[position++];
                    y = (sbyte)data[position++];
                    break;
                default:
                    return null;
            }

            SubChunk output = new() { Y = y };
            for (int i = 0; i < layerCount; i++)
            {
                output.Layers.Add(ReadLayer(data, ref position, y));
            }
            return output;
        }

        protected virtual PaletteSection<Block> ReadLayer(byte[] data, ref int position, sbyte y)
        {
            int bitCount = data[position++] >> 1;

            long[] words;
            int paletteLength;
            if (bitCount == 0)
            {
                // A uniform layer has no words and no palette length; its single entry follows directly
                words = [];
                paletteLength = 1;
            }
            else
            {
                int perWord = 32 / bitCount;
                int wordCount = (4096 + perWord - 1) / perWord;

                words = new long[wordCount];
                for (int i = 0; i < wordCount; i++)
                {
                    words[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                    position += 4;
                }

                paletteLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
                position += 4;
            }

            FastBufferProvider buffer = new(data, position);
            UnsafeNbtReader reader = new(buffer, ArrayPoolAllocation.CreateDefault()) { IsFormatLittleEndian = true };
            TagDeserializer deserializer = new();

            Block[] palette = new Block[paletteLength];
            for (int i = 0; i < paletteLength; i++)
            {
                palette[i] = ReadBlock(deserializer.Deserialize(reader));
            }
            position = buffer.Index;

            return new PaletteSection<Block>(palette, words)
            {
                Y = y,
                Locker = new BedrockBlockStateLocker(bitCount, paletteLength)
            };
        }

        protected virtual Block ReadBlock(Tag tag)
        {
            if (tag is not CompoundTag compound || compound["name"] is not StringTag nameTag) return new Block("minecraft:air");

            if (compound["states"] is not CompoundTag states || states.Count < 1) return new Block(nameTag);

            Property[] properties = new Property[states.Count];
            int i = 0;
            foreach (var state in states)
            {
                properties[i++] = new Property(state.Key, StateValue(state.Value));
            }
            return new Block(nameTag, properties);
        }
        private static string StateValue(Tag value)
        {
            return value.Id switch
            {
                TagId.String => (string)value,
                TagId.SignedByte => ((sbyte)value).ToString(),
                TagId.Int16 => ((short)value).ToString(),
                TagId.Int32 => ((int)value).ToString(),
                TagId.Int64 => ((long)value).ToString(),
                _ => string.Empty
            };
        }
    }
}
