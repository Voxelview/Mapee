using CommonUtilities.Pool;

namespace NbtEditor
{
    public class CompoundTagDeserializer : ITagDeserializer<CompoundTag>
    {
        public IIdTagDeserializer<Tag> IdTagDeserializer { get; set; }
        public IPool<CompoundTag> PreAllocatedCompoundTags { get; set; }

        /// <summary>
        /// Entries whose subtrees are skipped instead of deserialized, wherever they appear.
        /// Null means everything is kept.
        /// </summary>
        public IReadOnlySet<string>? SkipNames { get; set; }

        public CompoundTagDeserializer(IIdTagDeserializer<Tag> idTagDeserializer, IPool<CompoundTag> preAllocatedCompoundTags)
        {
            IdTagDeserializer = idTagDeserializer;
            PreAllocatedCompoundTags = preAllocatedCompoundTags;
        }

        public CompoundTag Deserialize(INbtReader reader)
        {
            CompoundTag output = PreAllocatedCompoundTags.Provide();
            output.Clear();

            while (true)
            {
                TagId elementId = (TagId) reader.ReadSignedByte();
                if(elementId == TagId.End) return output;

                string name = reader.ReadString();
                if (SkipNames is not null && SkipNames.Contains(name))
                {
                    NbtTagSkipper.Skip(reader, elementId);
                    continue;
                }

                Tag tag = IdTagDeserializer.Deserialize(reader, elementId);
                if (tag is null) continue;

                output.Add(name, tag);
            }
        }
    }
}
