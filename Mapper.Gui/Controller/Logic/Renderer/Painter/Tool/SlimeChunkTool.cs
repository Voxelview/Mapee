using Mapper.Gui.Model;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorldEditor;

namespace Mapper.Gui.Logic
{
    public class SlimeChunkTool : ToggleableTool, IPainter
    {
        public DrawingGroup? DrawingGroup
        {
            get => _drawingGroup;
            set
            {
                _drawingGroup = value;
                RenderOptions.SetBitmapScalingMode(value, BitmapScalingMode.NearestNeighbor);
                RenderOptions.SetEdgeMode(value, EdgeMode.Aliased);
            }
        }
        private DrawingGroup? _drawingGroup;

        public IScene Scene { get; }
        public ISlimeChunkChecker SlimeChunkChecker { get; set; }

        public Color ChunkColor { get; set; }
        public Pen OutlinePen { get; set; }
        public Pen OutlineHaloPen { get; set; }

        private static readonly int CHUNK_SIZE = 16;

        private static readonly byte FILL_ALPHA_ZOOMED_OUT = 232;
        private static readonly byte FILL_ALPHA_ZOOMED_IN = 168;

        private static readonly double FILL_FADE_START = 8D;
        private static readonly double FILL_FADE_END = 32D;

        private static readonly int MIN_OUTLINE_ZOOM_LEVEL = 0;
        private static readonly int MAX_OUTLINE_CHUNK_COUNT = 20000;

        private byte[]? _buffer = null;
        private bool[]? _slimeFlags = null;

        public SlimeChunkTool(IScene scene, ISlimeChunkChecker slimeChunkChecker)
        {
            Scene = scene;
            SlimeChunkChecker = slimeChunkChecker;

            ChunkColor = Color.FromRgb(102, 255, 51);

            SolidColorBrush outlineBrush = new(Color.FromArgb(255, 102, 255, 51));
            outlineBrush.Freeze();
            OutlinePen = new Pen(outlineBrush, 1);

            SolidColorBrush haloBrush = new(Color.FromArgb(210, 13, 38, 8));
            haloBrush.Freeze();
            OutlineHaloPen = new Pen(haloBrush, 3);

            Scene.ZoomChanged += Scene_ZoomChanged;
            Scene.DimensionChanged += Scene_DimensionChanged;

            Enabled = false;
        }

        public void Paint(DrawingContext drawingContext)
        {
            if (!Enabled || !IsTurnedOn || Scene.IsSceneEmpty) return;
            if (!IsZoomAppropriate()) return;

            Rect area = GetArea(CHUNK_SIZE);

            int xCount = IntervalMathUtilities.GetIntervalCount((int)area.X, (int)area.BottomRight.X, CHUNK_SIZE);
            int zCount = IntervalMathUtilities.GetIntervalCount((int)area.Y, (int)area.BottomRight.Y, CHUNK_SIZE);

            int xStart = MathUtilities.FindSectionY((int)area.X, CHUNK_SIZE);
            int zStart = MathUtilities.FindSectionY((int)area.Y, CHUNK_SIZE);

            int chunkCount = xCount * zCount;
            if (_buffer == null || _slimeFlags == null || chunkCount * 4 > _buffer.Length)
            {
                _buffer = new byte[chunkCount * 4];
                _slimeFlags = new bool[chunkCount];
            }

            byte[] pixelArray = _buffer;
            bool[] slimeFlags = _slimeFlags;

            double zoom = Scene.ZoomCoefficient;
            double chunkSizeOnScreen = CHUNK_SIZE * zoom;

            Color empty = Color.FromArgb(0, 0, 0, 0);
            Color fill = Color.FromArgb(GetFillAlpha(chunkSizeOnScreen), ChunkColor.R, ChunkColor.G, ChunkColor.B);

            Parallel.For(0, zCount, z =>
            {
                for (int x = 0; x < xCount; x++)
                {
                    int index = z * xCount + x;

                    bool isSlimeChunk = SlimeChunkChecker.IsSlimeChunk(xStart + x, zStart + z);
                    slimeFlags[index] = isSlimeChunk;

                    Color color = isSlimeChunk ? fill : empty;

                    int offset = index * 4;
                    pixelArray[offset] = color.B;
                    pixelArray[offset + 1] = color.G;
                    pixelArray[offset + 2] = color.R;
                    pixelArray[offset + 3] = color.A;
                }
            });

            BitmapSource bitmap = BitmapSource.Create(xCount, zCount, 96, 96, PixelFormats.Bgra32, null, pixelArray, xCount * 4);
            bitmap.Freeze();

            drawingContext.DrawImage(bitmap, new Rect(Scene.XzToPointOnScreen(new XzPoint((int)area.TopLeft.X, (int)area.TopLeft.Y)), new Size(xCount * CHUNK_SIZE * zoom, zCount * CHUNK_SIZE * zoom)));

            if (!IsOutlineAppropriate(chunkCount)) return;
            PaintOutline(drawingContext, slimeFlags, xCount, zCount, xStart, zStart, chunkSizeOnScreen);
        }
        private void PaintOutline(DrawingContext drawingContext, bool[] slimeFlags, int xCount, int zCount, int xStart, int zStart, double chunkSizeOnScreen)
        {
            StreamGeometry geometry = new();

            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                for (int z = 0; z < zCount; z++)
                {
                    for (int x = 0; x < xCount; x++)
                    {
                        if (!slimeFlags[z * xCount + x]) continue;

                        Point topLeft = Scene.XzToPointOnScreen(new XzPoint((xStart + x) * CHUNK_SIZE, (zStart + z) * CHUNK_SIZE));
                        Point bottomRight = Scene.XzToPointOnScreen(new XzPoint((xStart + x + 1) * CHUNK_SIZE, (zStart + z + 1) * CHUNK_SIZE));

                        geometryContext.BeginFigure(topLeft, false, true);
                        geometryContext.LineTo(new Point(bottomRight.X, topLeft.Y), true, false);
                        geometryContext.LineTo(bottomRight, true, false);
                        geometryContext.LineTo(new Point(topLeft.X, bottomRight.Y), true, false);
                    }
                }
            }

            geometry.Freeze();

            double thickness = GetOutlineThickness(chunkSizeOnScreen);

            // The dark halo goes down first and sticks out on both sides of the bright edge, so the
            // outline keeps its contrast over pale terrain the same way the fill keeps it over dark one.
            OutlineHaloPen.Thickness = thickness + 2;
            OutlinePen.Thickness = thickness;

            drawingContext.DrawGeometry(null, (Pen)OutlineHaloPen.GetAsFrozen(), geometry);
            drawingContext.DrawGeometry(null, (Pen)OutlinePen.GetAsFrozen(), geometry);
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

        private void Scene_ZoomChanged(object? sender, EventArgs e)
        {
            if (!Scene.IsSceneEmpty)
            {
                Enabled = Scene.Dimension == Dimension.Overworld && IsZoomAppropriate();
            }
            else
            {
                Enabled = false;
            }
        }
        private void Scene_DimensionChanged(object? sender, EventArgs e)
        {
            SetEnabled();
        }

        private void SetEnabled()
        {
            Enabled = Scene.Dimension == Dimension.Overworld;
        }
        private bool IsZoomAppropriate()
        {
            return Scene.ZoomLevel > -5;
        }
        private bool IsOutlineAppropriate(int chunkCount)
        {
            // Zoomed out further the chunks are too small for a border to read as anything but noise,
            // and stroking that many of them would cost more than it is worth while panning.
            return Scene.ZoomLevel >= MIN_OUTLINE_ZOOM_LEVEL && chunkCount <= MAX_OUTLINE_CHUNK_COUNT;
        }

        // Zoomed out a chunk is only a few pixels across, so the fill has to carry the visibility on its
        // own; zoomed in the outline takes that over and the fill can step back to show the map again.
        private static byte GetFillAlpha(double chunkSizeOnScreen)
        {
            double faded = Math.Clamp((chunkSizeOnScreen - FILL_FADE_START) / (FILL_FADE_END - FILL_FADE_START), 0D, 1D);
            return (byte)(FILL_ALPHA_ZOOMED_OUT - faded * (FILL_ALPHA_ZOOMED_OUT - FILL_ALPHA_ZOOMED_IN));
        }
        private static double GetOutlineThickness(double chunkSizeOnScreen)
        {
            if (chunkSizeOnScreen >= 96) return 3;
            if (chunkSizeOnScreen >= 32) return 2;

            return 1;
        }
    }
}
