using Mapper.Gui.Model;

namespace Mapper.Gui
{
    public static class ScrollbarUtilities
    {
        /// <summary>
        /// Pushes the scene's extents into the bar and reports whether there is anything to
        /// scroll. The caller uses that to hide the bar entirely - it overlays the map, so a
        /// track left showing with no thumb in it is a band of dimmed map for no reason.
        /// <para>
        /// This used to solve backwards for a viewport size that would make WPF's ScrollBar
        /// draw a thumb of the length we wanted. <see cref="MapScrollbar"/> measures its own
        /// thumb from the extents, so the extents are all it needs.
        /// </para>
        /// </summary>
        public static bool AdjustScrollbar(MapScrollbar scrollbarControl, IScrollbarWidget scrollbar)
        {
            if (scrollbar.LoadedArea is null || scrollbar.VisibleArea is null || scrollbar.LoadedArea.Value.IsEmpty())
            {
                return false;
            }

            // Order matters: the range has to be in place before Value, which clamps to it.
            scrollbarControl.Minimum = scrollbar.LoadedArea.Value.Point1;
            scrollbarControl.Maximum = scrollbar.LoadedArea.Value.Point2 - scrollbar.VisibleArea.Value.Size + 1;
            scrollbarControl.ViewportSize = scrollbar.VisibleArea.Value.Size;
            scrollbarControl.Value = scrollbar.VisibleArea.Value.Point1;

            return scrollbarControl.HasThumb;
        }
    }
}
