using System.Runtime.InteropServices;

namespace WorldEditor
{
    internal static class LibDeflate
    {
        public const string LibraryName = "deflate";

        private const CallingConvention Convention = CallingConvention.Cdecl;

        [DllImport(LibraryName, EntryPoint = "libdeflate_alloc_decompressor",
            CallingConvention = Convention, ExactSpelling = true)]
        public static extern LibDeflateDecompressorHandle AllocDecompressor();

        [DllImport(LibraryName, EntryPoint = "libdeflate_free_decompressor",
            CallingConvention = Convention, ExactSpelling = true)]
        public static extern void FreeDecompressor(IntPtr decompressor);

        [DllImport(LibraryName, EntryPoint = "libdeflate_zlib_decompress",
            CallingConvention = Convention, ExactSpelling = true)]
        public static extern LibDeflateResult ZLibDecompress(
            LibDeflateDecompressorHandle decompressor,
            ref byte input,
            nuint inputLength,
            ref byte output,
            nuint outputCapacity,
            out nuint actualOutputLength);

        [DllImport(LibraryName, EntryPoint = "libdeflate_gzip_decompress",
            CallingConvention = Convention, ExactSpelling = true)]
        public static extern LibDeflateResult GZipDecompress(
            LibDeflateDecompressorHandle decompressor,
            ref byte input,
            nuint inputLength,
            ref byte output,
            nuint outputCapacity,
            out nuint actualOutputLength);
    }
}
