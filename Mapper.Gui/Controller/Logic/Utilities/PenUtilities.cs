using System.Windows.Media;

namespace Mapper.Gui.Logic
{
    public static class PenUtilities
    {
        private static readonly Color HALO_COLOR = Color.FromRgb(8, 8, 10);
        private static readonly double HALO_THICKNESS = 2D;
        private static readonly double HALO_ALPHA_RATIO = 0.8D;

        public static Color CreateHaloColor(byte lineAlpha)
        {
            return Color.FromArgb((byte)(lineAlpha * HALO_ALPHA_RATIO), HALO_COLOR.R, HALO_COLOR.G, HALO_COLOR.B);
        }

        public static Pen CreateHaloPen(Pen linePen)
        {
            byte lineAlpha = linePen.Brush is SolidColorBrush lineBrush ? lineBrush.Color.A : byte.MaxValue;

            SolidColorBrush brush = new(CreateHaloColor(lineAlpha));
            brush.Freeze();

            Pen output = CreateHaloPen(linePen, brush);
            output.Freeze();

            return output;
        }
        public static Pen CreateHaloPen(Pen linePen, Brush brush)
        {
            Pen output = new(brush, linePen.Thickness + HALO_THICKNESS);
            if (linePen.DashStyle.Dashes.Count == 0) return output;

            double scale = linePen.Thickness / output.Thickness;

            DoubleCollection dashes = new();
            foreach (double dash in linePen.DashStyle.Dashes) dashes.Add(dash * scale);

            output.DashStyle = new DashStyle(dashes, linePen.DashStyle.Offset * scale);

            return output;
        }
    }
}
