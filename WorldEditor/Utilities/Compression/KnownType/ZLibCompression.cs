namespace WorldEditor
{
    public class ZLibCompression : IKnownTypeCompression
    {
        public int Compress(ArraySlice<byte> input, ArraySlice<byte> output)
        {
            return LibDeflateDecompression.DecompressZLib(input, output);
        }
    }
}
