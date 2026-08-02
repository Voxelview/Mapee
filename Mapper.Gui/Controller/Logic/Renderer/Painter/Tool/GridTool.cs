using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui.Logic
{
    public class GridTool : ToggleableTool, IPainter
    {
        public DrawingGroup? DrawingGroup
        {
            get => _drawingGroup;
            set
            {
                _drawingGroup = value;
                RenderOptions.SetEdgeMode(value, EdgeMode.Aliased);
            }
        }
        private DrawingGroup? _drawingGroup;

        public IScene Scene { get; }

        private Color _chunkLineColor;
        private readonly SolidColorBrush _chunkLinePenBrush;
        private readonly SolidColorBrush _chunkHaloPenBrush;

        private readonly SolidColorBrush _regionReallyThinLinPenBrush;
        private readonly SolidColorBrush _regionReallyThinHaloPenBrush;
        private Color _regionReallyThinLineColor;

        public Pen ChunkLinePen { get; set; }
        public Pen ChunkHaloPen { get; set; }

        public Pen RegionDashedLinePen { get; set; }
        public Pen RegionDashedHaloPen { get; set; }

        public Pen RegionDashedLinePenThin { get; set; }
        public Pen RegionDashedHaloPenThin { get; set; }

        public Pen RegionLinePenThin { get; set; }
        public Pen RegionHaloPenThin { get; set; }

        public Pen RegionLinePenReallyThin { get; set; }
        public Pen RegionHaloPenReallyThin { get; set; }

        private static readonly Color REGION_LINE_COLOR = Color.FromRgb(255, 176, 46);

        private static readonly double CHUNK_LINE_ZOOM_RATIO = 0.6D;
        private static readonly byte MAX_CHUNK_LINE_ALPHA = 150;
        private static readonly int MIN_CHUNK_HALO_ZOOM_LEVEL = 2;

        public GridTool(IScene scene)
        {
            Scene = scene;

            byte r = 255, a = 192;

            _chunkLineColor = Color.FromArgb(a, r, r, r);
            _chunkLinePenBrush = new SolidColorBrush(_chunkLineColor);
            ChunkLinePen = new Pen(_chunkLinePenBrush, 1);

            _chunkHaloPenBrush = new SolidColorBrush(PenUtilities.CreateHaloColor(_chunkLineColor.A));
            ChunkHaloPen = PenUtilities.CreateHaloPen(ChunkLinePen, _chunkHaloPenBrush);

            RegionDashedLinePen = new Pen(new SolidColorBrush(CreateRegionColor(235)), 3)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
            };
            RegionDashedHaloPen = PenUtilities.CreateHaloPen(RegionDashedLinePen);
            RegionDashedLinePen.Freeze();

            RegionDashedLinePenThin = new Pen(new SolidColorBrush(CreateRegionColor(235)), 2)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
            };
            RegionDashedHaloPenThin = PenUtilities.CreateHaloPen(RegionDashedLinePenThin);
            RegionDashedLinePenThin.Freeze();

            RegionLinePenThin = new Pen(new SolidColorBrush(CreateRegionColor(210)), 2);
            RegionHaloPenThin = PenUtilities.CreateHaloPen(RegionLinePenThin);
            RegionLinePenThin.Freeze();

            _regionReallyThinLineColor = CreateRegionColor(235);
            _regionReallyThinLinPenBrush = new SolidColorBrush(_regionReallyThinLineColor);
            RegionLinePenReallyThin = new Pen(_regionReallyThinLinPenBrush, 1);

            _regionReallyThinHaloPenBrush = new SolidColorBrush(PenUtilities.CreateHaloColor(_regionReallyThinLineColor.A));
            RegionHaloPenReallyThin = PenUtilities.CreateHaloPen(RegionLinePenReallyThin, _regionReallyThinHaloPenBrush);
        }

        public void Paint(DrawingContext drawingContext)
        {
            if (!Enabled || !IsTurnedOn) return;

            int zoomLevel = Scene.ZoomLevel;
            if (zoomLevel > -5)
            {
                double zoom = Scene.ZoomCoefficient * CHUNK_LINE_ZOOM_RATIO;

                byte a;
                if (zoom > 1) a = _chunkLineColor.A;
                else a = (byte)(_chunkLineColor.A * zoom);

                if (a >= MAX_CHUNK_LINE_ALPHA) a = MAX_CHUNK_LINE_ALPHA;

                _chunkLinePenBrush.Color = Color.FromArgb(a, _chunkLineColor.R, _chunkLineColor.G, _chunkLineColor.B);
                _chunkHaloPenBrush.Color = PenUtilities.CreateHaloColor(a);

                Pen? chunkHaloPen = zoomLevel >= MIN_CHUNK_HALO_ZOOM_LEVEL ? (Pen)ChunkHaloPen.GetAsFrozen() : null;
                RenderInterval(drawingContext, 16, (Pen)ChunkLinePen.GetAsFrozen(), chunkHaloPen, 512);
            }

            Pen regionLinePen, regionHaloPen;
            if (zoomLevel < -8)
            {
                double zoom = Scene.ZoomCoefficient * 5;
                byte a = (byte)Math.Min(_regionReallyThinLineColor.A * zoom, _regionReallyThinLineColor.A);

                _regionReallyThinLinPenBrush.Color = Color.FromArgb(a, _regionReallyThinLineColor.R, _regionReallyThinLineColor.G, _regionReallyThinLineColor.B);
                _regionReallyThinHaloPenBrush.Color = PenUtilities.CreateHaloColor(a);

                regionLinePen = (Pen)RegionLinePenReallyThin.GetAsFrozen();
                regionHaloPen = (Pen)RegionHaloPenReallyThin.GetAsFrozen();
            }
            else if (zoomLevel < -4)
            {
                regionLinePen = RegionLinePenThin;
                regionHaloPen = RegionHaloPenThin;
            }
            else if (zoomLevel < 0)
            {
                regionLinePen = RegionDashedLinePenThin;
                regionHaloPen = RegionDashedHaloPenThin;
            }
            else
            {
                regionLinePen = RegionDashedLinePen;
                regionHaloPen = RegionDashedHaloPen;
            }

            RenderInterval(drawingContext, 512, regionLinePen, regionHaloPen);
        }
        private void RenderInterval(DrawingContext drawingContext, int interval, Pen linePen, Pen? haloPen, int ignoreMod = 0)
        {
            Rect area = GetArea(interval);

            int xCount = IntervalMathUtilities.GetIntervalCount((int)area.X, (int)area.BottomRight.X, interval);
            int yCount = IntervalMathUtilities.GetIntervalCount((int)area.Y, (int)area.BottomRight.Y, interval);

            if (haloPen != null) RenderLines(drawingContext, area, interval, haloPen, xCount, yCount, ignoreMod);
            RenderLines(drawingContext, area, interval, linePen, xCount, yCount, ignoreMod);
        }
        private void RenderLines(DrawingContext drawingContext, Rect area, int interval, Pen pen, int xCount, int yCount, int ignoreMod)
        {
            for (int x = 0; x < xCount; x++)
            {
                XzPoint point0 = new((int)area.X + x * interval, (int)area.Y);
                XzPoint point1 = new((int)area.X + x * interval, (int)area.BottomRight.Y + 1);

                if (ignoreMod != 0 && MathUtilities.NegMod((int)point0.X, ignoreMod) == 0) continue;

                drawingContext.DrawLine(pen, Scene.XzToPointOnScreen(point0), Scene.XzToPointOnScreen(point1));
            }

            for (int y = 0; y < yCount; y++)
            {
                XzPoint point0 = new((int)area.X, (int)(area.Y + y * interval));
                XzPoint point1 = new((int)area.BottomRight.X + 1, (int)(area.Y + y * interval));

                if (ignoreMod != 0 && MathUtilities.NegMod((int)point0.Z, ignoreMod) == 0) continue;

                drawingContext.DrawLine(pen, Scene.XzToPointOnScreen(point0), Scene.XzToPointOnScreen(point1));
            }
        }

        private static Color CreateRegionColor(byte alpha)
        {
            return Color.FromArgb(alpha, REGION_LINE_COLOR.R, REGION_LINE_COLOR.G, REGION_LINE_COLOR.B);
        }

        private Rect GetArea(int interval)
        {
            Point topLeft = GetLocation(Scene.XzToXy(Scene.TopLeft), interval);

            XzPoint xzPoint = Scene.BottomRight;
            Point bottomRight = Scene.XzToXy(new XzPoint(xzPoint.X + 1, xzPoint.Z + 1));

            return new Rect(topLeft, bottomRight);
        }
        private static Point GetLocation(Point scenePoint, int interval)
        {
            return new Point(MathUtilities.FindSectionY((int)scenePoint.X, interval) * interval,
                MathUtilities.FindSectionY((int)scenePoint.Y, interval) * interval);
        }
    }
}
