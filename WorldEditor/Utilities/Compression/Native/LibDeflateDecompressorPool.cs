using System.Collections.Concurrent;

namespace WorldEditor
{
    public static class LibDeflateDecompressorPool
    {
        private static readonly ConcurrentBag<LibDeflateDecompressorHandle> Decompressors = new();

        public static LibDeflateDecompressorHandle Rent()
        {
            if (Decompressors.TryTake(out LibDeflateDecompressorHandle? pooled) && !pooled.IsInvalid)
            {
                return pooled;
            }

            LibDeflateDecompressorHandle created = LibDeflate.AllocDecompressor();
            if (created.IsInvalid) throw new OutOfMemoryException("libdeflate_alloc_decompressor returned NULL.");

            return created;
        }

        public static void Return(LibDeflateDecompressorHandle decompressor)
        {
            if (decompressor.IsInvalid) return;

            Decompressors.Add(decompressor);
        }
    }
}
