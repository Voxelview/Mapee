using System.Text;

namespace NbtEditor
{
    public class UnsafeNbtReader : INbtReader
    {
        public BufferProvider BufferProvider { get; set; }
        public bool IsFormatLittleEndian { get; set; } = false;

        public ArrayPoolAllocation PreAllocation { get; set; }

        public UnsafeNbtReader(BufferProvider bufferProvider, ArrayPoolAllocation preAllocation) 
        {
            BufferProvider = bufferProvider;
            PreAllocation = preAllocation;
        }
        public UnsafeNbtReader(byte[] bytes, int index = 0) : this(new FastBufferProvider(bytes, index), ArrayPoolAllocation.CreateDefault()) { }

        public sbyte ReadSignedByte()
        {
            BufferProvider.ProvideBuffer(sizeof(sbyte), out byte[] buffer, out int index);

            return (sbyte)buffer[index];
        }
        public unsafe short ReadInt16() 
        {
            BufferProvider.ProvideBuffer(sizeof(short), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index])
            {
                return PointerUtilities.ReadInt16(b, IsFormatLittleEndian);
            }
        }
        public unsafe ushort ReadUnsignedInt16()
        {
            BufferProvider.ProvideBuffer(sizeof(ushort), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index]) 
            {
                return PointerUtilities.ReadUnsignedInt16(b, IsFormatLittleEndian);
            }
        }
        public unsafe int ReadInt32() 
        {
            BufferProvider.ProvideBuffer(sizeof(int), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index]) 
            {
                return PointerUtilities.ReadInt32(b, IsFormatLittleEndian);
            }
        }
        public unsafe long ReadInt64() 
        {
            BufferProvider.ProvideBuffer(sizeof(long), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index]) 
            {
                return PointerUtilities.ReadInt64(b, IsFormatLittleEndian);
            }
        }
        public unsafe float ReadSingle() 
        {
            BufferProvider.ProvideBuffer(sizeof(float), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index])
            {
                return PointerUtilities.ReadSingle(b, IsFormatLittleEndian);
            }
        }
        public unsafe double ReadDouble()
        {
            BufferProvider.ProvideBuffer(sizeof(double), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index])
            {
                return PointerUtilities.ReadDouble(b, IsFormatLittleEndian);
            }
        }
        public unsafe string ReadString()
        {
            int length = ReadUnsignedInt16();
            if (length == 0) return string.Empty;
            BufferProvider.ProvideBuffer(length, out byte[] buffer, out int index);
            
            fixed (byte* b = &buffer[index]) 
            {
                return Encoding.UTF8.GetString(b, length);
            }
        }

        public sbyte[] ReadSignedByteArray(int length)
        {
            if (length == 0) return new sbyte[0];
            BufferProvider.ProvideBuffer(length, out byte[] buffer, out int index);

            sbyte[] output = PreAllocation.SBytePool.Provide(length);
            Buffer.BlockCopy(buffer, index, output, 0, length);

            return output;
        }
        public unsafe int[] ReadInt32Array(int length) 
        {
            if (length == 0) return Array.Empty<int>();
            BufferProvider.ProvideBuffer(length * sizeof(int), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index]) 
            {
                int[] output = PreAllocation.Int32Pool.Provide(length);
                PointerUtilities.ReadInt32Array(b, output, IsFormatLittleEndian);

                return output;
            }
        }
        public unsafe long[] ReadInt64Array(int length) 
        {
            if (length == 0) return new long[0];
            BufferProvider.ProvideBuffer(length * sizeof(long), out byte[] buffer, out int index);

            fixed (byte* b = &buffer[index]) 
            {
                long[] output = PreAllocation.Int64Pool.Provide(length);
                PointerUtilities.ReadInt64Array(b, output, IsFormatLittleEndian);

                return output;
            }
        }

        public void Skip(int byteCount)
        {
            if (byteCount <= 0) return;
            BufferProvider.ProvideBuffer(byteCount, out _, out _);
        }

        public void Dispose()
        {
            BufferProvider.Dispose();
        }
    }
}