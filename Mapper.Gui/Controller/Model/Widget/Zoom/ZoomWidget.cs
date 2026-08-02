using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui.Controller
{
    public class ZoomWidget : IZoomWidget
    {
        public ScaleBehaviour ScaleBehaviour { get; }
        public Control BaseControl { get; }

        public double ZoomPercentage => ScaleBehaviour.CurrentZoomCoefficient;

        public int Level => ScaleBehaviour.CurrentZoomLevel;
        public int MinLevel => ScaleBehaviour.MinZoomLevels;
        public int MaxLevel => ScaleBehaviour.MaxZoomLevels;

        public event EventHandler? LevelChanged;

        public ZoomWidget(ScaleBehaviour scaleBehaviour, Control baseControl)
        {
            ScaleBehaviour = scaleBehaviour;
            ScaleBehaviour.ZoomChanged += ScaleBehaviour_ZoomChanged;

            BaseControl = baseControl;
        }

        private void ScaleBehaviour_ZoomChanged(object? sender, EventArgs e)
        {
            LevelChanged?.Invoke(this, e);
        }

        public void ZoomIn()
        {
            ScaleBehaviour.ZoomIn(GetCenterPoint());
        }
        public void ZoomOut()
        {
            ScaleBehaviour.ZoomOut(GetCenterPoint());
        }

        /// <summary>
        /// Split by direction rather than passing a signed delta, because ScaleBehaviour's two
        /// methods each guard only their own end of the range: ZoomIn bails at the maximum and
        /// ZoomOut at the minimum, so a negative delta handed to the wrong one is dropped at a
        /// limit instead of moving back off it. Clamping first also matters - neither method
        /// re-checks per level, so an unclamped delta would sail straight past the bound.
        /// </summary>
        public void SetLevel(int level)
        {
            level = Math.Clamp(level, MinLevel, MaxLevel);

            int delta = level - ScaleBehaviour.CurrentZoomLevel;
            if (delta == 0) return;

            Point center = GetCenterPoint();

            if (delta > 0) ScaleBehaviour.ZoomIn(center, delta);
            else ScaleBehaviour.ZoomOut(center, -delta);
        }

        private Point GetCenterPoint()
        {
            return new Point(BaseControl.ActualWidth / 2, BaseControl.ActualHeight / 2);
        }
    }
}
