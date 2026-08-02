using System.Buffers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorldEditor;

namespace Mapper
{
    public class Canvas : ICanvas
    {
        /// <summary>
        /// A full region canvas is 512x512x4 = 1 MB, which lands on the large object heap.
        /// Allocating one per region meant roughly a gigabyte of large-object churn across
        /// a big load. Only a handful of canvases are alive at once, so a small dedicated
        /// pool covers it without the shared pool's per-core retention.
        /// </summary>
        private static readonly ArrayPool<byte> PixelPool = ArrayPool<byte>.Create(8 * 1024 * 1024, 16);

        public Coords TopLeftPoint { get; private set; }
        public Size Size { get; private set; }
        public Direction Direction { get; private set; }

        private readonly PixelTransformation _pixelTransformation;
        private readonly byte[] _pixelData;
        private readonly int _pixelLength;

        /// <summary>Row length in pixels, resolved once instead of per SetPixel call.</summary>
        private readonly int _rowStride;

        private bool _isEmpty = true;
        private bool _disposed;

        public Canvas(Coords topleft, Size size, Direction direction = Direction.North)
        {
            TopLeftPoint = topleft;
            Size = size;
            Direction = direction;

            _pixelTransformation = new PixelTransformation(Direction, TopLeftPoint, Size);

            _rowStride = (int)size.Height;
            _pixelLength = (int)size.Width * (int)size.Height * 4;
            _pixelData = PixelPool.Rent(_pixelLength);

            // Rented buffers still hold the previous region's pixels. Any pixel the
            // renderer does not write has to read back as fully transparent.
            Array.Clear(_pixelData, 0, _pixelLength);
        }

        public void SetPixel(int xInWorld, int zInWorld, VecRgb color)
        {
            _pixelTransformation.TransformPixelCoords(xInWorld, zInWorld, out int xP, out int yP);

            int offset = (yP * _rowStride + xP) * 4;
            Rgb rgb = color.ToByteRgb();

            _pixelData[offset] = rgb.B;
            _pixelData[offset + 1] = rgb.G;
            _pixelData[offset + 2] = rgb.R;
            _pixelData[offset + 3] = byte.MaxValue;
            _isEmpty = false;
        }

        public ImageSource? GetBitmap()
        {
            if (_isEmpty) return null;

            WriteableBitmap output = new((int)Size.Height, (int)Size.Width, 96, 96, PixelFormats.Bgra32, null);
            output.Lock();

            int stride = (int)output.Width * (output.Format.BitsPerPixel / 8);
            output.WritePixels(new Int32Rect(0, 0, (int)output.Width, (int)output.Height), _pixelData, stride, 0);
            
            output.Unlock();
            output.Freeze();

            return output;
        }

        /// <summary>
        /// Returns the pixel buffer to the pool. Callers must be done with the canvas -
        /// <see cref="GetBitmap"/> copies into the bitmap, so the bitmap stays valid.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            PixelPool.Return(_pixelData);
        }
    }
}
