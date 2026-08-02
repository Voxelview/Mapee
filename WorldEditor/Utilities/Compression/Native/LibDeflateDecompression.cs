using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WorldEditor
{
    public static class LibDeflateDecompression
    {
        public const int InvalidSlice = -2;
        public const int BadData = -3;
        public const int ShortOutput = -4;
        public const int InsufficientSpace = -5;

        public static int DecompressZLib(ArraySlice<byte> input, ArraySlice<byte> output)
        {
            return Decompress(input, output, false);
        }
        public static int DecompressGZip(ArraySlice<byte> input, ArraySlice<byte> output)
        {
            return Decompress(input, output, true);
        }

        private static int Decompress(ArraySlice<byte> input, ArraySlice<byte> output, bool gzip)
        {
            if (!IsInBounds(input) || !IsInBounds(output)) return InvalidSlice;
            if (input.Length == 0) return BadData;
            if (output.Length == 0) return InsufficientSpace;

            ref byte inputReference = ref Reference(input);
            ref byte outputReference = ref Reference(output);

            LibDeflateDecompressorHandle decompressor = LibDeflateDecompressorPool.Rent();

            try
            {
                LibDeflateResult result;
                nuint written;

                if (gzip)
                {
                    result = LibDeflate.GZipDecompress(decompressor,
                        ref inputReference, (nuint)input.Length,
                        ref outputReference, (nuint)output.Length,
                        out written);
                }
                else
                {
                    result = LibDeflate.ZLibDecompress(decompressor,
                        ref inputReference, (nuint)input.Length,
                        ref outputReference, (nuint)output.Length,
                        out written);
                }

                switch (result)
                {
                    case LibDeflateResult.Success: return (int)written;
                    case LibDeflateResult.ShortOutput: return ShortOutput;
                    case LibDeflateResult.InsufficientSpace: return InsufficientSpace;
                    default: return BadData;
                }
            }
            finally
            {
                LibDeflateDecompressorPool.Return(decompressor);
            }
        }

        private static ref byte Reference(ArraySlice<byte> slice)
        {
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slice.Array), slice.Position);
        }

        private static bool IsInBounds(ArraySlice<byte> slice)
        {
            return slice.Array is not null
                && slice.Position >= 0
                && slice.Length >= 0
                && slice.Position <= slice.Array.Length - slice.Length;
        }
    }
}
