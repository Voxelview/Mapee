using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System.Windows;

namespace Mapper.Gui.Controller
{
    public class GoToTool : ToggleableTool
    {
        /// <summary>
        /// The element the popup drops under. This was the toolbar button that owned the tool;
        /// the tool now lives in the title bar, so it takes whatever hosts it and only needs a
        /// position and a size - not a <see cref="ToolButton"/>.
        /// </summary>
        public FrameworkElement? Anchor { get; set; }

        public Scene Scene { get; }
        public CanvasControl Canvas { get; }

        private bool _isOpen = false;

        public GoToTool(Scene scene, CanvasControl canvas)
        {
            Scene = scene;
            Canvas = canvas;
        }

        /// <summary>
        /// The point the window opens on, and the one it re-centres relative to: the middle of
        /// the canvas, which is what the title bar reads out.
        /// </summary>
        public XzPoint CenterPoint => Scene.Map.TransformPointOnScreenToXz(new Point(Canvas.ActualWidth / 2, Canvas.ActualHeight / 2));

        public void OpenWindow()
        {
            // Clicking the anchor while the window is up deactivates the window, which closes
            // it - so without this the same click would immediately reopen one.
            if (_isOpen || Anchor is null) return;
            _isOpen = true;

            Point startupLocation = Anchor.PointToScreen(new(0, 0)).CalibrateToDpiScale();

            XzPoint playerPos = new(), playSpawn = new(), worldSpawn = new();
            if (Scene.Domain.CurrentWorld is not null)
            {
                playerPos = XzPoint.FromVector(Scene.Domain.CurrentWorld.Level.Player.Position);
                playSpawn = XzPoint.FromVector(Scene.Domain.CurrentWorld.Level.Player.Spawn);
                worldSpawn = XzPoint.FromVector(Scene.Domain.CurrentWorld.Level.WorldGen.WorldSpawn);
            }

            GoToWindow goToWindow = new(CenterPoint, playerPos, playSpawn, worldSpawn);

            startupLocation.Y += Anchor.ActualHeight;
            startupLocation.X += Anchor.ActualWidth / 2 - goToWindow.Width / 2;

            goToWindow.Top = startupLocation.Y;
            goToWindow.Left = startupLocation.X;

            goToWindow.Show();
            goToWindow.Closing += (s, ee) =>
            {
                _isOpen = false;

                if (goToWindow.DialogClosed) return;
                Scene.Map.SetCenterPoint(goToWindow.SelectedPoint);
            };
        }
    }
}
