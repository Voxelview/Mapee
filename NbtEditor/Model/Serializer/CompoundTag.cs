using CommonUtilities.Collections.Simple;

namespace NbtEditor 
{
    public class CompoundTagSerializer : ITagSerializer<CompoundTag> 
    {
        public ITagSerializer<Tag> IdTagSerializer { get; set; }

        public CompoundTagSerializer(ITagSerializer<Tag> idTagSerializer)
        {
            IdTagSerializer = idTagSerializer;
        }

        public void Serialize(CompoundTag tag, INbtWriter writer)
        {
            for (int i = 0; i < tag.Count; i++)
            {
                KeyValueEntry<string, Tag> pair = tag.EntryAt(i);

                writer.WriteSignedByte((sbyte)pair.Value.Id);
                writer.WriteString(pair.Key);
                IdTagSerializer.Serialize(pair.Value, writer);
            }

            writer.WriteSignedByte((sbyte)TagId.End);
        }
    }
}
