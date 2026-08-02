using System.Windows.Media;

namespace Mapper.Gui
{
    public struct MouseColorProperties
    {
        public SolidColorBrush Default { get; set; }
        public SolidColorBrush MouseOver { get; set; }
        public SolidColorBrush MouseDown { get; set; }

        /// <summary>
        /// Outlines for the same three states. A fill alone has to be bright to register against
        /// a panel this dark, which makes an ordinary hover look like a selection; carrying the
        /// state on an outline instead lets the fill stay quiet.
        /// </summary>
        public SolidColorBrush DefaultBorder { get; set; }
        public SolidColorBrush MouseOverBorder { get; set; }
        public SolidColorBrush MouseDownBorder { get; set; }

        /// <summary>
        /// How strongly the accent is laid over the glyph across those same three states, 0 for
        /// none. The icon warms as the fill and the outline brighten, so the button moves as one
        /// piece instead of leaving a cold white glyph in the middle of a lit frame.
        /// <para>
        /// Deliberately partial at every step. The white image underneath keeps drawing the icon
        /// and this only warms it; at full strength the overlay would cover the image and the
        /// icon's edges would come from however crisply the mask rasterized instead.
        /// </para>
        /// </summary>
        public double DefaultTint { get; set; }
        public double MouseOverTint { get; set; }
        public double MouseDownTint { get; set; }
    }
}
