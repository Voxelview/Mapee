using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// The map's scrollbar, drawn rather than templated.
    /// <para>
    /// WPF's <see cref="System.Windows.Controls.Primitives.ScrollBar"/> positions its thumb
    /// through a <see cref="System.Windows.Controls.Primitives.Track"/>, which places it at a
    /// fractional offset derived from the scroll value. A thumb whose bounds land between device
    /// pixels has nowhere whole to put a one-pixel edge, so the outline came out missing a side -
    /// and which side moved as you scrolled. Nothing reachable from the template fixes that,
    /// because the geometry is decided above it.
    /// </para>
    /// <para>
    /// Here the geometry is the control's own: the thumb is measured and drawn on whole pixels,
    /// with the stroke inset half a pixel so a 1-wide pen covers exactly one column. The
    /// viewport maths goes with it - the old path had to solve backwards for a viewport size
    /// that would yield the thumb length it wanted, where this takes the sizes directly.
    /// </para>
    /// </summary>
    public class MapScrollbar : Control
    {
        /// <summary>Short of this the thumb stops being usable as a drag target.</summary>
        private const double MIN_THUMB_LENGTH = 18;

        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <summary>Lowest scroll position, in world units.</summary>
        public double Minimum { get; set; }

        /// <summary>Highest scroll position, in world units.</summary>
        public double Maximum { get; set; }

        /// <summary>How much of the world is on screen, in the same units.</summary>
        public double ViewportSize { get; set; }

        public double Value
        {
            get => _value;
            set
            {
                double clamped = Clamp(value);
                if (_value == clamped) return;

                _value = clamped;
                InvalidateVisual();
            }
        }
        private double _value;

        public Brush ThumbBrush { get; set; } = Brushes.Gray;
        public Brush ThumbHoverBrush { get; set; } = Brushes.Gray;
        public Brush ThumbDragBrush { get; set; } = Brushes.Gray;

        public Brush ThumbBorderBrush { get; set; } = Brushes.White;
        public Brush ThumbBorderHoverBrush { get; set; } = Brushes.White;
        public Brush ThumbBorderDragBrush { get; set; } = Brushes.White;

        public event EventHandler<double>? ValueChanged;

        private bool _isDragging = false;
        private bool _isOverThumb = false;

        /// <summary>Where inside the thumb the drag started, so it does not jump to the cursor.</summary>
        private double _grabOffset = 0;

        public MapScrollbar()
        {
            // Without a brush the control is not hit-testable, and the track would swallow
            // nothing - clicks would fall through to the map underneath.
            Background = Brushes.Transparent;

            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
        }

        /// <summary>The travel available, in world units. Zero when everything already fits.</summary>
        public double Range => Math.Max(0, Maximum - Minimum);

        /// <summary>False when there is nothing to scroll, which is when the bar should not show.</summary>
        public bool HasThumb => Range > 0 && ViewportSize > 0;

        private double TrackLength => Orientation == Orientation.Horizontal ? ActualWidth : ActualHeight;
        private double Thickness => Orientation == Orientation.Horizontal ? ActualHeight : ActualWidth;

        private double Clamp(double value)
        {
            if (Maximum <= Minimum) return Minimum;
            return Math.Clamp(value, Minimum, Maximum);
        }

        /// <summary>
        /// Length and offset both rounded to whole pixels before anything is drawn - this is the
        /// point of the class.
        /// </summary>
        private bool TryGetThumb(out double offset, out double length)
        {
            offset = 0;
            length = 0;

            double track = TrackLength;
            if (!HasThumb || track <= 0) return false;

            double total = Range + ViewportSize;
            if (total <= 0) return false;

            length = Math.Round(ViewportSize / total * track);
            length = Math.Max(MIN_THUMB_LENGTH, Math.Min(length, track));

            double travel = track - length;
            offset = travel <= 0 ? 0 : Math.Round((Value - Minimum) / Range * travel);
            offset = Math.Clamp(offset, 0, travel);

            return true;
        }

        private Rect GetThumbRect(double offset, double length)
        {
            return Orientation == Orientation.Horizontal
                ? new Rect(offset, 0, length, Thickness)
                : new Rect(0, offset, Thickness, length);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

            if (!TryGetThumb(out double offset, out double length)) return;

            Brush fill = _isDragging ? ThumbDragBrush : _isOverThumb ? ThumbHoverBrush : ThumbBrush;
            Brush edge = _isDragging ? ThumbBorderDragBrush : _isOverThumb ? ThumbBorderHoverBrush : ThumbBorderBrush;

            // Half a pixel in on every side: a 1-wide pen straddles the line it is given, so an
            // un-inset rect would paint half its stroke outside the thumb, where it is clipped
            // away. This is what kept losing an edge.
            Rect thumb = GetThumbRect(offset, length);
            Rect stroked = new Rect(thumb.X + 0.5, thumb.Y + 0.5, Math.Max(0, thumb.Width - 1), Math.Max(0, thumb.Height - 1));

            Pen pen = new Pen(edge, 1);
            pen.Freeze();

            drawingContext.DrawRectangle(fill, pen, stroked);
        }

        /// <summary>
        /// When this bar overlaps the window's resize border, only the thumb takes the mouse.
        /// <para>
        /// <c>WindowChrome.IsHitTestVisibleInChrome</c> is all-or-nothing per element, so
        /// flagging the bar handed it the whole edge and the window stopped resizing there.
        /// Declining the hit everywhere except the thumb puts the rest back: the hit test falls
        /// through to the map, which makes no such claim, and the chrome keeps the edge.
        /// </para>
        /// <para>
        /// The cost is that clicking the track no longer pages - along that edge a click reaches
        /// the map instead. Only the bar that sits on the border pays it; the other keeps normal
        /// hit testing, and its track still pages.
        /// </para>
        /// </summary>
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (!System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(this))
            {
                return base.HitTestCore(hitTestParameters);
            }

            Point point = hitTestParameters.HitPoint;
            if (!TryGetThumb(out double offset, out double length)) return null;
            if (!GetThumbRect(offset, length).Contains(point)) return null;

            return new PointHitTestResult(this, point);
        }

        private bool IsOverThumb(Point point)
        {
            if (!TryGetThumb(out double offset, out double length)) return false;
            return GetThumbRect(offset, length).Contains(point);
        }

        /// <summary>Turns a cursor position into the scroll value that puts the thumb there.</summary>
        private double ValueFromPosition(double position, double length)
        {
            double travel = TrackLength - length;
            if (travel <= 0) return Minimum;

            return Minimum + Math.Clamp(position / travel, 0, 1) * Range;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (!TryGetThumb(out double offset, out double length)) return;

            Point point = e.GetPosition(this);
            double along = Orientation == Orientation.Horizontal ? point.X : point.Y;

            if (GetThumbRect(offset, length).Contains(point))
            {
                _grabOffset = along - offset;
            }
            else
            {
                // Clicking the track centres the thumb on the cursor and drags from there, so a
                // click and a click-drag do the same thing.
                _grabOffset = length / 2;
                SetValue(ValueFromPosition(along - _grabOffset, length));
            }

            _isDragging = true;
            CaptureMouse();
            InvalidateVisual();

            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point point = e.GetPosition(this);

            if (!_isDragging)
            {
                bool over = IsOverThumb(point);
                if (over == _isOverThumb) return;

                _isOverThumb = over;
                InvalidateVisual();
                return;
            }

            if (!TryGetThumb(out _, out double length)) return;

            double along = Orientation == Orientation.Horizontal ? point.X : point.Y;
            SetValue(ValueFromPosition(along - _grabOffset, length));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();

            _isOverThumb = IsOverThumb(e.GetPosition(this));
            InvalidateVisual();

            e.Handled = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (_isDragging || !_isOverThumb) return;

            _isOverThumb = false;
            InvalidateVisual();
        }

        /// <summary>
        /// Moves the bar and reports it. Separate from the <see cref="Value"/> setter so the
        /// widget can push a position in without being told about its own change.
        /// </summary>
        private void SetValue(double value)
        {
            double clamped = Clamp(value);
            if (_value == clamped) return;

            _value = clamped;
            InvalidateVisual();

            ValueChanged?.Invoke(this, _value);
        }

        /// <summary>Re-measures the thumb against a size the control has only just been given.</summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            InvalidateVisual();
        }
    }
}
