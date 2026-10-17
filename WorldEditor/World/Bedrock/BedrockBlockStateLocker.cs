namespace WorldEditor
{
    // Bedrock packs palette indexes into 32-bit words without spanning words, in XZY order.
    // The words are kept as stored, one per long, and only decoded into Mapee's YZX order when unlocked
    public class BedrockBlockStateLocker : Locker
    {
        public override int BitCount { get; }
        public override int UnlockedArrayLength => 16 * 16 * 16;

        protected int PaletteLength { get; }

        public BedrockBlockStateLocker(int bitCount, int paletteLength)
        {
            BitCount = bitCount;
            PaletteLength = paletteLength;
        }

        public override long[] Lock(short[] unlockedArray)
        {
            throw new NotSupportedException("Bedrock sections are read-only");
        }
        public override void Unlock(long[] lockedArray, short[] outputArray)
        {
            if (BitCount == 0)
            {
                Array.Clear(outputArray, 0, UnlockedArrayLength);
                return;
            }

            int perWord = 32 / BitCount;
            uint mask = (1u << BitCount) - 1;

            for (int i = 0; i < UnlockedArrayLength; i++)
            {
                uint word = (uint)lockedArray[i / perWord];
                int value = (int)((word >> (i % perWord * BitCount)) & mask);
                if (value >= PaletteLength) value = 0;

                int x = i >> 8, z = (i >> 4) & 15, y = i & 15;
                outputArray[y << 8 | z << 4 | x] = (short)value;
            }
        }
    }
}
