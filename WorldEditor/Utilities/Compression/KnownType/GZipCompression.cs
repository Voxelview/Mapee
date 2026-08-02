namespace WorldEditor
{
    public class GZipCompression : IKnownTypeCompression
    {
        public int Compress(ArraySlice<byte> input, ArraySlice<byte> output)
        {
            return LibDeflateDecompression.DecompressGZip(input, output);
        }
    }
}
