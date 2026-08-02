using Mapper.Gui.Controller;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Logic
{
    public class Renderer
    {
        public CanvasControl GraphicsCanvas { get; }
        public Scene Scene { get; }

        public IBackgroundPainter BackgroundPainter { get; }
        public IScenePainter ScenePainter { get; }

        public event RenderUpdate? OnRenderUpdate;

        private readonly IList<IPainter> _painters;
        private bool _isBusy = false;

        public Renderer(Scene scene, CanvasControl graphicsCanvas) 
        {
            GraphicsCanvas = graphicsCanvas;
            Scene = scene;
            _painters = new List<IPainter>();

            BackgroundPainter = new BackgroundPainter(new TilePainter(), new SolidColorBackgroundPainter(), this);
            AddPainter(BackgroundPainter);

            ScenePainter = new ScenePainter(this);
            AddPainter(ScenePainter);

            AddScaleEvents();

            GraphicsCanvas.SizeChanged += GraphicsCanvas_SizeChanged;

            Scene.WorldBeginChange += Scene_WorldBeginChange;
            Scene.WorldChanged += Scene_WorldChanged;

            Scene.DimensionBeginChange += Scene_DimensionBeginChange;
            Scene.DimensionChanged += Scene_DimensionChanged;
        }

        public void AddPainter(IPainter painter)
        {
            painter.DrawingGroup = GraphicsCanvas.ProvideNextDrawingGroup();
            _painters.Add(painter);
        }
        public void Render()
        {
            GraphicsCanvas.Dispatcher.Invoke(() =>
            {
                if (_isBusy) return;
                _isBusy = true;

                foreach (IPainter painter in _painters)
                {
                    if (painter.DrawingGroup is null) continue;

                    using DrawingContext drawingContext = painter.DrawingGroup.Open();
                    painter.Paint(drawingContext);
                }

                _isBusy = false;
                OnRenderUpdate?.Invoke();
            });
        }

        private void AddScaleEvents()
        {
            Scene.Map.ScaleBehaviour.OffsetChanged += ScaleBehaviour_OffsetChanged;
            Scene.Map.ScaleBehaviour.ZoomChanged += ScaleBehaviour_ZoomChanged;
        }
        private void RemoveScaleEvents()
        {
            Scene.Map.ScaleBehaviour.OffsetChanged -= ScaleBehaviour_OffsetChanged;
            Scene.Map.ScaleBehaviour.ZoomChanged -= ScaleBehaviour_ZoomChanged;
        }

        private void GraphicsCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Render();
        }

        private void ScaleBehaviour_OffsetChanged(object? sender, EventArgs e)
        {
            if (Scene.Domain.CurrentWorld is not null)
            {
                Scene.Map.ScaleBehaviour.SaveScaling(Scene.Domain.CurrentWorld.CurrentDimension.Scaling);
            }

            Render();
        }
        private void ScaleBehaviour_ZoomChanged(object? sender, EventArgs e)
        {
            if (Scene.Domain.CurrentWorld is not null)
            {
                Scene.Map.ScaleBehaviour.SaveScaling(Scene.Domain.CurrentWorld.CurrentDimension.Scaling);
            }

            Render();
        }
        private void Dimension_Update(object? sender, EventArgs e)
        {
            Render();
        }

        private void Scene_WorldBeginChange(WorldDomain? old, WorldDomain current)
        {
            if (old is not null)
            {
                old.CurrentDimension.Scene.Update -= Dimension_Update;
            }

            RemoveScaleEvents();
            Render();
        }
        private void Scene_WorldChanged(WorldDomain? old, WorldDomain current)
        {
            current.CurrentDimension.Scene.Update += Dimension_Update;
            Scene.Map.ScaleBehaviour.SetScaling(current.CurrentDimension.Scaling);

            AddScaleEvents();
            Render();
        }

        private void Scene_DimensionBeginChange(DimensionDomain old, DimensionDomain current)
        {
            old.Scene.Update -= Dimension_Update;
            RemoveScaleEvents();
        }
        private void Scene_DimensionChanged(DimensionDomain old, DimensionDomain current)
        {
            Scene.Map.ScaleBehaviour.SetScaling(current.Scaling);

            current.Scene.Update += Dimension_Update;
            AddScaleEvents();
            Render();
        }
    }
}
