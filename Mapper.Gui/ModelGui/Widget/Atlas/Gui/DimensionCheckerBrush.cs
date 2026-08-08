using Mapper.Gui.Model;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui
{
    /// <summary>
    /// The checker a plate sits on, in the same pair the map paints its own empty ground with -
    /// ColorPair.Overworld, .Nether and .TheEnd. So a Nether plate sits on the Nether's dark red
    /// and an End plate on its purple, and the picker looks like the thing it is picking.
    ///
    /// Built once per dimension and frozen. The tile is deliberately small: at this size it is
    /// texture rather than pattern, and the two cells are only about five perceived levels apart
    /// to begin with.
    /// </summary>
    public static class DimensionCheckerBrush
    {
        private const double CELL = 4;

        private static readonly Dictionary<Dimension, Brush> CACHE = new();
        private static readonly object GATE = new();

        public static Brush For(Dimension dimension)
        {
            lock (GATE)
            {
                if (CACHE.TryGetValue(dimension, out Brush? cached)) return cached;

                Brush brush = Create(PairFor(dimension));
                CACHE[dimension] = brush;

                return brush;
            }
        }

        private static ColorPair PairFor(Dimension dimension)
        {
            if (dimension == Dimension.Nether) return ColorPair.Nether;
            if (dimension == Dimension.TheEnd) return ColorPair.TheEnd;

            return ColorPair.Overworld;
        }

        private static Brush Create(ColorPair colors)
        {
            GeometryGroup odd = new();
            odd.Children.Add(new RectangleGeometry(new Rect(CELL, 0, CELL, CELL)));
            odd.Children.Add(new RectangleGeometry(new Rect(0, CELL, CELL, CELL)));

            DrawingGroup drawing = new();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Even), null,
                new RectangleGeometry(new Rect(0, 0, CELL * 2, CELL * 2))));
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Odd), null, odd));

            DrawingBrush output = new(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, CELL * 2, CELL * 2),
                ViewportUnits = BrushMappingMode.Absolute
            };

            output.Freeze();

            return output;
        }
    }
}
