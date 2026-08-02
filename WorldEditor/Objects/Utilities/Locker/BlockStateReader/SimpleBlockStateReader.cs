namespace WorldEditor
{
    public class SimpleBlockStateReader : IBlockStateReader
    {
        /// <summary>
        /// Unpacks palette indices stored <paramref name="bitCount"/> bits at a time, with no
        /// entry straddling a long boundary.
        /// </summary>
        /// <remarks>
        /// This runs over 4096 blocks for every section the scanner touches, making it one of
        /// the hottest loops in a load. Extracting with a mask costs one shift instead of two,
        /// and hoisting the output bound out of the inner loop drops a branch per element.
        /// </remarks>
        public void Read(long[] array, int bitCount, short[] outputArray)
        {
            if (bitCount <= 0)
            {
                // A single-entry palette needs no bits, so every index is 0. The previous
                // version looped on a zero step here and filled the array with a garbage value.
                Array.Clear(outputArray);
                return;
            }

            int perLong = 64 / bitCount;
            if (perLong == 0) return;

            ulong mask = bitCount >= 64 ? ulong.MaxValue : (1UL << bitCount) - 1;

            int outputIndex = 0;
            int total = outputArray.Length;

            for (int i = 0; i < array.Length && outputIndex < total; i++)
            {
                ulong l = (ulong)array[i];

                int count = total - outputIndex;
                if (count > perLong) count = perLong;

                for (int j = 0, shift = 0; j < count; j++, shift += bitCount)
                {
                    outputArray[outputIndex + j] = (short)((l >> shift) & mask);
                }

                outputIndex += count;
            }
        }

        public short ReadSingle(long[] array, int bitCount, int index)
        {
            int fitBits = 64 / bitCount;
            int arrayIndex = index / fitBits;
            ulong l = (ulong)array[arrayIndex];

            int moveBy = 64 - bitCount;
            return (short)(ushort)(l << (moveBy - (index % fitBits * bitCount)) >> moveBy);
        }
    }
}
