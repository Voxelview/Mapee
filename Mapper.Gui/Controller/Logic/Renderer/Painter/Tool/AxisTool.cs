using Mapper.Gui.Model;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Logic
{
    public class AxisTool : ToggleableTool, IPainter
    {
        public DrawingGroup? DrawingGroup { get; set; }
        public IScene Scene { get; }

        public Pen XAxisPen { get; set; }
        public Pen ZAxisPen { get; set; }

        public Pen XAxisHaloPen { get; set; }
        public Pen ZAxisHaloPen { get; set; }

        public Brush TextBackgroundBrush { get; set; }
        public Brush XTextBrush { get; set; }
        public Brush ZTextBrush { get; set; }

        public Thickness Padding { get; set; } = new Thickness(97, 45, 5, 0);

        private static readonly Color X_AXIS_COLOR = Color.FromRgb(255, 48, 24);
        private static readonly Color Z_AXIS_COLOR = Color.FromRgb(64, 226, 255);

        private const string POS_X_LABEL = "+X [East]";
        private const string Neg_X_LABEL = "-X [West]";
        private const string POS_Z_LABEL = "+Z [South]";
        private const string NEG_Z_LABEL = "-Z [North]";

        private FormattedText POS_X_TEXT, NEG_X_TEXT, POS_Z_TEXT, NEG_Z_TEXT;

        public AxisTool(IScene scene)
        {
            Scene = scene;

            XAxisPen = new Pen(new SolidColorBrush(X_AXIS_COLOR), 4);
            XAxisPen.Freeze();

            ZAxisPen = new Pen(new SolidColorBrush(Z_AXIS_COLOR), 4);
            ZAxisPen.Freeze();

            XAxisHaloPen = PenUtilities.CreateHaloPen(XAxisPen);
            ZAxisHaloPen = PenUtilities.CreateHaloPen(ZAxisPen);

            TextBackgroundBrush = new SolidColorBrush(Color.FromArgb(248, 24, 24, 24));
            TextBackgroundBrush.Freeze();

            XTextBrush = new SolidColorBrush(X_AXIS_COLOR);
            XTextBrush.Freeze();

            ZTextBrush = new SolidColorBrush(Z_AXIS_COLOR);
            ZTextBrush.Freeze();

            POS_X_TEXT = CreateText(POS_X_LABEL, XTextBrush);
            NEG_X_TEXT = CreateText(Neg_X_LABEL, XTextBrush);
            POS_Z_TEXT = CreateText(POS_Z_LABEL, ZTextBrush);
            NEG_Z_TEXT = CreateText(NEG_Z_LABEL, ZTextBrush);
        }

        private static FormattedText CreateText(string value, Brush brush)
        {
            return new FormattedText(
                value,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                20,
                brush,
                96);
        }

        public void Paint(DrawingContext drawingContext)
        {
            if (!Enabled || !IsTurnedOn) return;

            XzRange areaInScene = GetAreaInScene();
            Rect areaOnScreen = GetAreaOnScreen(areaInScene);

            double x = Scene.XzToPointOnScreen(new XzPoint(areaInScene.TopLeftPoint.X, 0)).Y;
            double z = Scene.XzToPointOnScreen(new XzPoint(0, areaInScene.TopLeftPoint.Z)).X;

            DrawLines(x, z, areaOnScreen, drawingContext);
            DrawLabels(x, z, areaOnScreen, drawingContext);
        }

        private XzRange GetAreaInScene()
        {
            return new XzRange
            (
                Scene.TopLeft,
                Scene.BottomRight
            );
        }
        private Rect GetAreaOnScreen(XzRange areaInScene)
        {
            Rect output = new(Scene.XzToPointOnScreen(areaInScene.TopLeftPoint), Scene.XzToPointOnScreen(areaInScene.BottomRightPoint));
            output.Width++;
            output.Height++;

            return output;
        }

        private void DrawLines(double x, double z, Rect areaOnScreen, DrawingContext drawingContext)
        {
            Point zPoint0 = new(z, 0), zPoint1 = new(z, areaOnScreen.Height);
            Point xPoint0 = new(0, x), xPoint1 = new(areaOnScreen.Width, x);

            drawingContext.DrawLine(ZAxisHaloPen, zPoint0, zPoint1);
            drawingContext.DrawLine(XAxisHaloPen, xPoint0, xPoint1);

            drawingContext.DrawLine(ZAxisPen, zPoint0, zPoint1);
            drawingContext.DrawLine(XAxisPen, xPoint0, xPoint1);
        }
        private void DrawLabels(double x, double z, Rect areaOnScreen, DrawingContext drawingContext)
        {
            FormattedText negZText = NEG_Z_TEXT;
            DrawText(drawingContext, new Point(z - negZText.Width / 2, Padding.Top), negZText);

            FormattedText posZText = POS_Z_TEXT;
            DrawText(drawingContext, new Point(z - posZText.Width / 2, areaOnScreen.Height - posZText.Height - Padding.Bottom), posZText);

            FormattedText negXText = NEG_X_TEXT;
            DrawText(drawingContext, new Point(Padding.Left, x - (negXText.Height / 2)), negXText);

            FormattedText posXText = POS_X_TEXT;
            DrawText(drawingContext, new Point(areaOnScreen.Width - posXText.Width - Padding.Right, x - (posXText.Height / 2)), posXText);
        }

        private void DrawText(DrawingContext drawingContext, Point point, FormattedText text)
        {
            DrawTextBackground(drawingContext, point, text);
            drawingContext.DrawText(text, point);
        }
        private void DrawTextBackground(DrawingContext drawingContext, Point point, FormattedText text)
        {
            drawingContext.DrawRectangle(TextBackgroundBrush, null, new Rect(point.X - 5, point.Y - 1, text.Width + 10, text.Height + 1));
        }
    }
}
