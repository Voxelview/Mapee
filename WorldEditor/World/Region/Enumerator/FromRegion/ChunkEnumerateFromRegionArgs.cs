namespace WorldEditor
{
    public readonly struct ChunkEnumerateFromRegionArgs
    {
        public byte[] RegionBuffer { get; }

        /// <summary>
        /// Bytes of <see cref="RegionBuffer"/> that actually came from the region file. The buffer is
        /// rented and so is usually longer; anything past this point is another region's leftovers.
        /// </summary>
        public int DataLength { get; }

        public Coords[] ChunksToRead { get; }
        public StorageFormat StorageFormat { get; }

        public ChunkEnumerateFromRegionArgs(byte[] regionBuffer, int dataLength, Coords[] chunksToRead, StorageFormat storageFormat)
        {
            RegionBuffer = regionBuffer;
            DataLength = dataLength;
            ChunksToRead = chunksToRead;
            StorageFormat = storageFormat;
        }
    }
}
