namespace WorldEditor
{
    public static class LibDeflateDecompressorPool
    {
        /// <summary>
        /// One decompressor per thread. Decompressors are stateless between calls, and a
        /// shared bag meant a synchronized take/add pair per chunk across every worker.
        /// The handle lives as long as its thread; SafeHandle finalization reclaims it if
        /// the thread dies.
        /// </summary>
        [ThreadStatic]
        private static LibDeflateDecompressorHandle? t_decompressor;

        public static LibDeflateDecompressorHandle Rent()
        {
            LibDeflateDecompressorHandle? pooled = t_decompressor;
            if (pooled is not null && !pooled.IsInvalid)
            {
                return pooled;
            }

            LibDeflateDecompressorHandle created = LibDeflate.AllocDecompressor();
            if (created.IsInvalid) throw new OutOfMemoryException("libdeflate_alloc_decompressor returned NULL.");

            t_decompressor = created;
            return created;
        }

        public static void Return(LibDeflateDecompressorHandle decompressor)
        {
            // Thread-affine now; nothing to give back.
        }
    }
}
