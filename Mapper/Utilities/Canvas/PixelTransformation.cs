using System.Runtime.CompilerServices;
using System.Windows;
using WorldEditor;

namespace Mapper
{
    /// <summary>
    /// Maps world coordinates to pixel coordinates for a canvas facing a given direction.
    /// </summary>
    /// <remarks>
    /// Called once per rendered pixel - on the order of a quarter of a billion times over a
    /// large load - so the origins are resolved to ints up front. Reading them off
    /// <see cref="Size"/> per call cost a double-to-int conversion on every pixel. The
    /// properties are get-only because the precomputed origins would otherwise go stale.
    /// </remarks>
    public class PixelTransformation
    {
        public Direction Direction { get; }
        public Coords TopLeft { get; }
        public Size Size { get; }

        private readonly int _xForwardOrigin;
        private readonly int _xReverseOrigin;
        private readonly int _zForwardOrigin;
        private readonly int _zReverseOrigin;

        public PixelTransformation(Direction direction, Coords topLeft, Size size)
        {
            Direction = direction;
            TopLeft = topLeft;
            Size = size;

            int width = (int)size.Width;
            int height = (int)size.Height;

            _xForwardOrigin = topLeft.X - height + 1;
            _xReverseOrigin = topLeft.X;
            _zForwardOrigin = topLeft.Z;
            _zReverseOrigin = topLeft.Z + width - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformPixelCoords(int xInWorld, int zInWorld, out int x, out int y)
        {
            switch (Direction)
            {
                case Direction.North:
                    x = xInWorld - _xForwardOrigin;
                    y = zInWorld - _zForwardOrigin;
                    break;
                case Direction.East:
                    x = zInWorld - _zForwardOrigin;
                    y = _xReverseOrigin - xInWorld;
                    break;
                case Direction.South:
                    x = _xReverseOrigin - xInWorld;
                    y = _zReverseOrigin - zInWorld;
                    break;
                default:
                    x = _zReverseOrigin - zInWorld;
                    y = xInWorld - _xForwardOrigin;
                    break;
            }
        }
    }
}
