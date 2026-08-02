using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Controller
{
    public class PositionWidget : IPositionWidget
    {
        public GoToTool GoTo { get; }

        public XzPoint CenterPoint => GoTo.CenterPoint;
        public ImageSource? Icon { get; }

        public event EventHandler? CenterChanged;

        public PositionWidget(Scene scene, CanvasControl canvas)
        {
            GoTo = new GoToTool(scene, canvas);
            Icon = ToolButtonIcons.Get("GoTo");

            // The centre moves for two reasons and neither reports it directly: panning shifts
            // the offset, and zooming keeps the offset but changes how much world that covers.
            scene.Map.ScaleBehaviour.OffsetChanged += Map_Changed;
            scene.Map.ScaleBehaviour.ZoomChanged += Map_Changed;
        }

        private void Map_Changed(object? sender, EventArgs e)
        {
            CenterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void OpenGoTo(FrameworkElement anchor)
        {
            GoTo.Anchor = anchor;
            GoTo.OpenWindow();
        }
    }
}
