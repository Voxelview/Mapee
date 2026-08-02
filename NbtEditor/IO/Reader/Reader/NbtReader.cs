using System.Buffers.Binary;
using System.Text;

namespace NbtEditor
{
    public class NbtReader : INbtReader 
    {
        public BufferProvider BufferProvider { get; set; }
        public bool IsFormatLittleEndian { get; set; } = false;

        public ArrayPoolAllocation PreAllocation { get; set; }

        public NbtReader(BufferProvider bufferProvider) 
        {
            BufferProvider = bufferProvider;
            PreAllocation = ArrayPoolAllocation.CreateDefault();
        }
        public NbtReader(BufferProvider bufferProvider, ArrayPoolAllocation preAllocation)
        {
            BufferProvider = bufferProvider;
            PreAllocation = preAllocation;
        }

        public sbyte ReadSignedByte()
        {
            BufferProvider.ProvideBuffer(sizeof(sbyte), out byte[] buffer, out int index);

            return (sbyte)buffer[index];
        }
        public short ReadInt16()
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(short));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadInt16LittleEndian(buffer);
            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }
        public ushort ReadUnsignedInt16()
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(ushort));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }
        public int ReadInt32() 
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(int));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadInt32LittleEndian(buffer);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }
        public long ReadInt64() 
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(long));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadInt64LittleEndian(buffer);
            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }
        public float ReadSingle() 
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(float));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadSingleLittleEndian(buffer);
            return BinaryPrimitives.ReadSingleBigEndian(buffer);
        }
        public double ReadDouble()
        {
            ReadOnlySpan<byte> buffer = ProvideBuffer(sizeof(double));

            if (IsFormatLittleEndian) return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
            return BinaryPrimitives.ReadDoubleBigEndian(buffer);
        }
        private readonly StringPool _stringPool = new();

        public string ReadString()
        {
            int length = ReadUnsignedInt16();
            if (length == 0) return string.Empty;

            return _stringPool.GetOrAdd(ProvideBuffer(length));
        }

        public sbyte[] ReadSignedByteArray(int length)
        {
            if (length == 0) return Array.Empty<sbyte>();
            BufferProvider.ProvideBuffer(length, out byte[] buffer, out int index);

            sbyte[] output = PreAllocation.SBytePool.Provide(length);
            Buffer.BlockCopy(buffer, index, output, 0, length);

            return output;
        }
        public int[] ReadInt32Array(int length) 
        {
            if (length == 0) return Array.Empty<int>();

            int[] output = PreAllocation.Int32Pool.Provide(length);

            for (int i = 0; i < length; i++)
            {
                output[i] = ReadInt32();
            }

            return output;
        }
        public long[] ReadInt64Array(int length)
        {
            if (length == 0) return Array.Empty<long>();

            long[] output = PreAllocation.Int64Pool.Provide(length);

            for (int i = 0; i < length; i++) 
            {
                output[i] = ReadInt64();
            }

            return output;
        }

        public void Skip(int byteCount)
        {
            if (byteCount <= 0) return;
            BufferProvider.ProvideBuffer(byteCount, out _, out _);
        }

        private ReadOnlySpan<byte> ProvideBuffer(int count)
        {
            BufferProvider.ProvideBuffer(count, out byte[] buffer, out int index);
            return new ReadOnlySpan<byte>(buffer, index, count);
        }

        public void Dispose()
        {
            BufferProvider.Dispose();
        }
    }
}
