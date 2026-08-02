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
            if (icon is not DrawingImage) return null;

            return new Size(icon.Width * (SIZE / GRID), icon.Height * (SIZE / GRID));
        }
    }
}
