using System.Buffers;

namespace WorldEditor
{
    /// <summary>
    /// Buffers sized to hold a whole region file.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="ArrayPool{T}.Shared"/>. These buffers are megabytes each,
    /// and the shared pool keeps them alive in per-core stacks, so they stay in the working
    /// set long after a load finishes. Only a few regions are ever read at once, so a small
    /// bounded pool gets the same allocation savings with a predictable ceiling.
    /// </remarks>
    public static class RegionBufferPool
    {
        private const int MaxRegionBytes = 64 * 1024 * 1024;

        /// <summary>
        /// Headroom over the number of regions read at once. If this matched the concurrency
        /// exactly, any transient extra return would be dropped and re-allocated next time.
        /// </summary>
        private const int MaxBuffersPerBucket = 16;

        public static ArrayPool<byte> Instance { get; } = ArrayPool<byte>.Create(MaxRegionBytes, MaxBuffersPerBucket);
    }
}
