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
                Block? block = ReadBlock(input[i]);
                if (block is null) continue;

                output[i] = block.Value;
            }

            return output;
        }

        protected virtual Block? ReadBlock(Tag tag)
        {
            if (tag is StringTag blockStringTag) return new Block(blockStringTag, []);
            if (tag is not CompoundTag blockTag) return null;

            Tag? nameTag = blockTag["Name"] ?? blockTag["id"];
            if (nameTag is null)
            {
                Tag? compactTag = blockTag[string.Empty];
                return compactTag is null ? null : ReadBlock(compactTag);
            }

            if (nameTag.Id != TagId.String) return null;

            string name = nameTag;
            Property[] properties;

            Tag? propertiesTag = blockTag["Properties"] ?? blockTag["properties"];
            if (propertiesTag is CompoundTag propertiesCompound)
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
                KeyValueEntry<string, Tag> pair = propertiesTag.ElementAt(i);
                properties[i] = new Property(pair.Key, pair.Value);
            }

            return properties;
        }
    }
}
