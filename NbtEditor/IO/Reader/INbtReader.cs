namespace NbtEditor
{
    public interface INbtReader : IDisposable
    {
        sbyte ReadSignedByte();
        short ReadInt16();
        ushort ReadUnsignedInt16();
        int ReadInt32();
        long ReadInt64();
        float ReadSingle();
        double ReadDouble();
        string ReadString();
        sbyte[] ReadSignedByteArray(int length);
        int[] ReadInt32Array(int length);
        long[] ReadInt64Array(int length);

        /// <summary>Advances past <paramref name="byteCount"/> bytes without materializing them.</summary>
        void Skip(int byteCount);
    }
}
