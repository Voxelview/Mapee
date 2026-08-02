namespace NbtEditor
{
    /// <summary>
    /// Walks past a tag's payload using only length fields, so an unwanted subtree costs a
    /// few reads instead of pooled tags, array copies and string decodes. Corrupt negative
    /// lengths skip nothing; the surrounding parse then fails the same way it does today.
    /// </summary>
    public static class NbtTagSkipper
    {
        public static long SkipCount;

        public static void Skip(INbtReader reader, TagId id)
        {
            SkipCount++;
            switch (id)
            {
                case TagId.SignedByte: reader.Skip(sizeof(sbyte)); break;
                case TagId.Int16: reader.Skip(sizeof(short)); break;
                case TagId.Int32: reader.Skip(sizeof(int)); break;
                case TagId.Int64: reader.Skip(sizeof(long)); break;
                case TagId.Single: reader.Skip(sizeof(float)); break;
                case TagId.Double: reader.Skip(sizeof(double)); break;
                case TagId.String: reader.Skip(reader.ReadUnsignedInt16()); break;
                case TagId.SignedByteArray: reader.Skip(reader.ReadInt32()); break;
                case TagId.Int32Array: reader.Skip(reader.ReadInt32() * sizeof(int)); break;
                case TagId.Int64Array: reader.Skip(reader.ReadInt32() * sizeof(long)); break;
                case TagId.List: SkipList(reader); break;
                case TagId.Compound: SkipCompound(reader); break;
            }
        }

        private static void SkipList(INbtReader reader)
        {
            TagId elementId = (TagId)reader.ReadSignedByte();
            int count = reader.ReadInt32();
            if (count < 1) return;

            switch (elementId)
            {
                case TagId.SignedByte: reader.Skip(count); return;
                case TagId.Int16: reader.Skip(count * sizeof(short)); return;
                case TagId.Int32: reader.Skip(count * sizeof(int)); return;
                case TagId.Int64: reader.Skip(count * sizeof(long)); return;
                case TagId.Single: reader.Skip(count * sizeof(float)); return;
                case TagId.Double: reader.Skip(count * sizeof(double)); return;
            }

            for (int i = 0; i < count; i++)
            {
                Skip(reader, elementId);
            }
        }

        private static void SkipCompound(INbtReader reader)
        {
            while (true)
            {
                TagId id = (TagId)reader.ReadSignedByte();
                if (id == TagId.End) return;

                reader.Skip(reader.ReadUnsignedInt16());
                Skip(reader, id);
            }
        }
    }
}
