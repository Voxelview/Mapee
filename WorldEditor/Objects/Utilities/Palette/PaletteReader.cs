using CommonUtilities.Collections.Simple;
using NbtEditor;

namespace WorldEditor
{
    public class PaletteReader : IObjectReader<ListTag, Block[]>
    {
        public Block[] Read(ListTag input)
        {
            Block[] output = new Block[input.Count];

            for (int i = 0; i < input.Count; i++)
            {
                if (input[i] is not CompoundTag blockTag) continue;

                Block? block = ReadBlock(blockTag);
                if (block is null) continue;

                output[i] = block.Value;
            }

            return output;
        }

        protected virtual Block? ReadBlock(CompoundTag blockTag)
        {
            Tag? nameTag = blockTag["Name"];
            if (nameTag is null || nameTag.Id != TagId.String) return null;

            string name = nameTag;
            Property[] properties;

            if (blockTag.TryGetValue("Properties", out Tag? propertiesTag) && propertiesTag is CompoundTag propertiesCompound)
            {
                properties = ReadProperties(propertiesCompound);
            }
            else
            {
                properties = Array.Empty<Property>();
            }

            return new Block(name, properties);
        }
        protected virtual Property[] ReadProperties(CompoundTag propertiesTag)
        {
            Property[] properties = new Property[propertiesTag.Count];

            for (int i = 0; i < propertiesTag.Count; i++)
            {
                KeyValueEntry<string, Tag> pair = propertiesTag.EntryAt(i);
                properties[i] = new Property(pair.Key, pair.Value);
            }

            return properties;
        }
    }
}
