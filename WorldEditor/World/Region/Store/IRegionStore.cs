namespace WorldEditor
{
    public interface IRegionStore
    {
        int Count { get; }

        IEnumerable<Coords> Itemize(string directory);
        bool Exists(Coords coords);

        /// <summary>
        /// Reads a region into a buffer rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/>.
        /// On success the caller owns the buffer and must return it to the shared pool. Only the first
        /// <paramref name="length"/> bytes are valid; the rest is whatever the pool last held.
        /// </summary>
        bool GetData(Coords coords, out byte[]? buffer, out int length, out StorageFormat format);
    }
}
