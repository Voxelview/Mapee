using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui
{
    public static class GlyphIcon
    {
        private const double GRID = 24;
        private const double SIZE = 18;

        public static Size? Measure(ImageSource? icon)
        {
            return Measure(icon, SIZE);
        }

        /// <summary>
        /// Sizes a glyph to something other than the toolbar's 18. Off that size the 1.333
        /// strokes stop landing on whole pixels, so this is for glyphs shown large enough that
        /// it does not read - the rail's top button, which is scaled to sit beside the 25-grid
        /// dimension art rather than beside the other glyphs.
        /// </summary>
        public static Size? Measure(ImageSource? icon, double size)
        {
            if (icon is not DrawingImage) return null;

            return new Size(icon.Width * (size / GRID), icon.Height * (size / GRID));
        }
    }
}
