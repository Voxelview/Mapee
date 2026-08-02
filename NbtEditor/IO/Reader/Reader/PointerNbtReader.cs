using System.Text;

namespace NbtEditor 
{
    public unsafe class PointerNbtReader : INbtReader 
    {
        public bool IsFormatLittleEndian { get; set; } = false;

        public ArrayPoolAllocation PreAllocation { get; set; }

        private byte* _ptr;
        
        public PointerNbtReader(byte* ptr, ArrayPoolAllocation preAllocation) 
        { 
            _ptr = ptr;
            PreAllocation = preAllocation;
        }

        public sbyte ReadSignedByte()
        {
            return *(sbyte*)_ptr++;
        }
        public short ReadInt16() 
        {
            short output = PointerUtilities.ReadInt16(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(short);

            return output;
        }
        public ushort ReadUnsignedInt16()
        {
            ushort output = PointerUtilities.ReadUnsignedInt16(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(ushort);

            return output;
        }
        public int ReadInt32()
        {
            int output = PointerUtilities.ReadInt32(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(int);

            return output;
        }
        public long ReadInt64()
        {
            long output = PointerUtilities.ReadInt64(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(long);

            return output;
        }
        public float ReadSingle() 
        {
            float output = PointerUtilities.ReadSingle(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(float);

            return output;
        }
        public double ReadDouble()
        {
            double output = PointerUtilities.ReadDouble(_ptr, IsFormatLittleEndian);
            _ptr += sizeof(double);

            return output;
        }
        public string ReadString()
        {
            int length = ReadUnsignedInt16();
            if (length == 0) return string.Empty;

            string output = Encoding.UTF8.GetString(_ptr, length);
            _ptr += length;

            return output;
        }

        public sbyte[] ReadSignedByteArray(int length) 
        {
            if (length == 0) return Array.Empty<sbyte>();

            sbyte[] output = PreAllocation.SBytePool.Provide(length);
            fixed (sbyte* b = output) {
                Buffer.MemoryCopy(_ptr, b, length, length);
            }

            _ptr += length;

            return output;
        }
        public int[] ReadInt32Array(int length) 
        {
            if (length == 0) return Array.Empty<int>();

            int[] output = PreAllocation.Int32Pool.Provide(length);
            PointerUtilities.ReadInt32Array(_ptr, output, IsFormatLittleEndian);
            _ptr += sizeof(int) * length;

            return output;
        }
        public long[] ReadInt64Array(int length) 
        {
            if (length == 0) return Array.Empty<long>();

            long[] output = PreAllocation.Int64Pool.Provide(length);
            PointerUtilities.ReadInt64Array(_ptr, output, IsFormatLittleEndian);
            _ptr += sizeof(long) * length;

            return output;
        }

        public void Skip(int byteCount)
        {
            if (byteCount <= 0) return;
            _ptr += byteCount;
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
        }
    }
}
