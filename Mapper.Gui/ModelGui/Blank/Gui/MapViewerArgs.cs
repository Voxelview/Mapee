using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    public class MapViewerArgs
    {
        public Control Canvas { get; set; }
        public Control Footer { get; set; }
        public Control HorizontalScrollbar { get; set; }
        public Control VerticalScrollbar { get; set; }
        public Control Rail { get; set; }

        /// <summary>
        /// Content for the window title bar. Everything else here goes into a fixed cell of
        /// the window body; this one is handed to the frame template instead, which is why it
        /// is optional - the frame is shared with dialogs that have nothing to put there.
        /// </summary>
        public FrameworkElement? TitleBarContent { get; set; }

        public IList<Control> Widgets { get; set; }

        public MapViewerArgs(Control canvas, Control footer, Control horizontalScrollbar, Control verticalScrollbar, Control rail)
        {
            Canvas = canvas;
            Footer = footer;
            HorizontalScrollbar = horizontalScrollbar;
            VerticalScrollbar = verticalScrollbar;
            Rail = rail;

            Widgets = new List<Control>();
        }
    }
}
