using Mapper.Gui.Logic;
using System.Windows;
using System.Windows.Controls;
using WorldEditor;

namespace Mapper.Gui.Controller
{
    public class Controller
    {
        public ProgramDomain Domain { get; }
        public Scene ImplementedScene { get; }
        public Renderer ImplementedRenderer { get; }

        public CanvasControl GraphicsCanvas { get; }
        public InformationWidget InformationWidget { get; }
        public ScrollbarWidget HorizontalScrollbarWidget { get; }
        public ScrollbarWidget VerticalScrollbarWidget { get; }

        public ToolbarWidget ToolbarWidget { get; private set; }
        public ZoomWidget ZoomWidget { get; private set; }
        public PositionWidget PositionWidget { get; private set; }
        public DimensionWidget DimensionWidget { get; private set; }
        public StylebarWidget StylebarWidget { get; private set; }
        public AtlasWidget AtlasWidget { get; }

        public TextPainter TextPainter { get; }

        public MapViewer MainWindow { get; }

        public Controller() 
        {
            GraphicsCanvas = new CanvasControl(8);

            Domain = new ProgramDomain();
            ImplementedScene = new Scene(GraphicsCanvas, Domain);
            ImplementedRenderer = new Renderer(ImplementedScene, GraphicsCanvas);

            InformationWidget = new InformationWidget(ImplementedScene, ImplementedRenderer);
            HorizontalScrollbarWidget = new ScrollbarWidget(ImplementedScene, ImplementedRenderer, Orientation.Horizontal);
            VerticalScrollbarWidget = new ScrollbarWidget(ImplementedScene, ImplementedRenderer, Orientation.Vertical);

            MouseHook.Start();

            // Before CreateMainWindowArgs, which builds the rail, which builds BrowseTool - and
            // that tool is now nothing but a switch on this.
            AtlasWidget = new AtlasWidget(ImplementedScene, new LevelReader());

            // Started before the window is even built, so plates are already landing by the time
            // the picker opens. Nothing here blocks: the directory listing is inside the scan task
            // too, because a saves folder on a network drive would otherwise hold the window up.
            AtlasWidget.Refresh();

            MainWindow = new MapViewer();
            MainWindow.Initialize(CreateMainWindowArgs());

            TextPainter = new TextPainter(new RenderInvoker(ImplementedRenderer), GraphicsCanvas);
            ImplementedRenderer.AddPainter(TextPainter);

            ImplementedScene.WorldBeginChange += Scene_WorldBeginChange;
            ImplementedScene.WorldChanged += Scene_WorldChanged;
            ImplementedScene.DimensionBeginChange += Scene_DimensionBeginChange;
            ImplementedScene.StyleBeginReset += Scene_StyleReset;
        }

        private MapViewerArgs CreateMainWindowArgs()
        {
            MapViewerArgs args = new(
                GraphicsCanvas,
                new InformationControl(InformationWidget),
                new HorizontalScrollbarControl(HorizontalScrollbarWidget),
                new VerticalScrollbarControl(VerticalScrollbarWidget),
                CreateRailControl());

            args.TitleBarContent = CreateTitleBarContent();
            args.Widgets.Add(CreateStylebarControl());

            // Last, and it has to stay last. MapViewer.Initialize gives every widget the same
            // ZIndex of 100, so within the canvas cell they are ordered by their position in this
            // list alone - and the picker has to come out over the style chip, not under it.
            args.Widgets.Add(new AtlasControl(AtlasWidget));

            return args;
        }

        /// <summary>
        /// Position and zoom, in that order, for the right end of the title bar. Both describe
        /// where you are looking rather than what is drawn, which is why neither is on the rail.
        /// </summary>
        private FrameworkElement CreateTitleBarContent()
        {
            StackPanel output = new()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            output.Children.Add(CreatePositionControl());
            output.Children.Add(CreateZoomControl());

            return output;
        }

        private Control CreateZoomControl()
        {
            ZoomWidget = new ZoomWidget(ImplementedScene.Map.ScaleBehaviour, GraphicsCanvas);

            return new ZoomControl(ZoomWidget);
        }
        private Control CreatePositionControl()
        {
            PositionWidget = new PositionWidget(ImplementedScene, GraphicsCanvas);

            return new PositionControl(PositionWidget)
            {
                Margin = new Thickness(0, 0, 10, 0)
            };
        }
        private Control CreateStylebarControl()
        {
            StylebarWidget = new StylebarWidget(ImplementedScene);

            return new StylebarControl(StylebarWidget)
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(7, 0, 0, 7)
            };
        }

        /// <summary>
        /// The rail owns the dimension button as well as the tools: it is the one control that
        /// says which world you are looking at, so it sits above them rather than among them.
        /// </summary>
        private Control CreateRailControl()
        {
            ToolbarWidget = new ToolbarWidget(MainWindow, ImplementedRenderer, AtlasWidget);
            DimensionWidget = new DimensionWidget(ImplementedScene);

            RailControl output = new(ToolbarWidget);
            output.SetHeader(new DimensionControl(DimensionWidget));

            return output;
        }

        private void Scene_WorldBeginChange(WorldDomain? old, WorldDomain current)
        {
            TextPainter.SetText(null);

            TextPainter.SetText(GetDimensionText(current.CurrentDimension) ?? new Text("Loading world"));
        }
        private void Scene_WorldChanged(WorldDomain? old, WorldDomain current)
        {
            if (current.Level.Version.Version == Version.Pre_Beta_1_2)
            {
                StylebarWidget?.SelectedStyleId = "alpha";
            }
        }

        private void Scene_DimensionBeginChange(DimensionDomain old, DimensionDomain current) 
        {
            CheckDimensionIsEmpty(current);
        }
        private void Scene_StyleReset(Logic.Style old, Logic.Style current)
        {
            if (ImplementedScene.Domain.CurrentWorld is null) return;
            CheckDimensionIsEmpty(ImplementedScene.Domain.CurrentWorld.CurrentDimension);
        }

        private void CheckDimensionIsEmpty(DimensionDomain current)
        {
            TextPainter.SetText(GetDimensionText(current));
        }

        private IText? GetDimensionText(DimensionDomain current)
        {
            if (!ImplementedScene.Domain.CurrentStyle.IsDimensionAllowed(current.Dimension))
            {
                return new Text($"{current.Dimension.Name} dimension does not support the current selected style");
            }

            int count = current.Scene.SceneParameter.RegionStore.Count;
            if (count < 1)
            {
                return new Text($"{current.Dimension.Name} dimension is empty");
            }
            else if (current.Dimension == Dimension.Overworld &&
                current.Scene.RenderedRegions.Count == 0 &&
                current.Scene.SceneParameter.Level.Version.Version == Version.Pre_Beta_1_2)
            {
                return new Text("Loading Alpha chunks. This may take a moment");
            }

            return null;
        }
    }
}
