using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Model
{
    public interface IPositionWidget
    {
        /// <summary>
        /// Where the middle of the map currently is. This, rather than the cursor, is what the
        /// Go-to window opens on and re-centres relative to - so the readout and what clicking
        /// it does describe the same point.
        /// </summary>
        XzPoint CenterPoint { get; }

        ImageSource? Icon { get; }

        /// <summary>
        /// Raised whenever the map is panned or zoomed. Fires at mouse-move rate during a drag,
        /// so a view drawing from it has to throttle.
        /// </summary>
        event EventHandler? CenterChanged;

        /// <summary>
        /// Opens the Go-to window beneath <paramref name="anchor"/>.
        /// </summary>
        void OpenGoTo(FrameworkElement anchor);
    }
}
