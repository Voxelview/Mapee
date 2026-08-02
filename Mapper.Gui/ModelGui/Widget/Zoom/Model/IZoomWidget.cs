using System;

namespace Mapper.Gui.Model
{
    public interface IZoomWidget
    {
        double ZoomPercentage { get; }

        /// <summary>
        /// Zoom as a discrete step rather than a coefficient. The map has always zoomed in whole
        /// levels between <see cref="MinLevel"/> and <see cref="MaxLevel"/>; a slider needs to see
        /// that number, because the coefficient it drives is exponential in it.
        /// </summary>
        int Level { get; }
        int MinLevel { get; }
        int MaxLevel { get; }

        event EventHandler? LevelChanged;

        void ZoomIn();
        void ZoomOut();

        /// <summary>
        /// Jumps straight to a level, clamped to the range. Raises <see cref="LevelChanged"/>
        /// like any other zoom.
        /// </summary>
        void SetLevel(int level);
    }
}
