using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System.Collections.Generic;

namespace Mapper.Gui.Controller
{
    public class ToolbarWidget : IToolbarWidget
    {
        public IList<IToolButtonSegment> ToolButtonSegments { get; } = new List<IToolButtonSegment>();

        public MapViewer MainWindow { get; }
        public Renderer Renderer { get; }
        public ToolScene ToolScene { get; }
        public IRenderInvoker RenderInvoker { get; }

        public ToolbarWidget(MapViewer mainWindow, Renderer renderer) 
        {
            MainWindow = mainWindow;
            Renderer = renderer;
            ToolScene = new ToolScene(Renderer.Scene);
            RenderInvoker = new RenderInvoker(Renderer);

            ToolButtonSegment shortSegment = new();
            shortSegment.Tools.Add(CreateGridToolButton());
            shortSegment.Tools.Add(CreateSlimeChunkButton());
            shortSegment.Tools.Add(CreateAxisButton());
            shortSegment.Tools.Add(CreateChunkHighlightsButton());
            shortSegment.Tools.Add(CreateMeasureLengthButton());
            shortSegment.Tools.Add(CreateDayNightCycleButton());
            shortSegment.Tools.Add(CreateGoToButton());
            shortSegment.Tools.Add(CreateExportAsImageButton());

            ToolButtonSegment longSegment = new()
            {
                LeftGap = 63
            };

            longSegment.Tools.Add(CreateFilterButton());
            longSegment.Tools.Add(CreateRenderSettingsButton());
            longSegment.Tools.Add(CreateBrowseButton());

            ToolButtonSegments.Add(shortSegment);
            ToolButtonSegments.Add(longSegment);
        }

        private ToolButton CreateGridToolButton() 
        {
            GridTool tool = new(ToolScene);
            Renderer.AddPainter(tool);
            return CreateButton(tool, "Gridlines", "Grid lines");
        }
        private ToolButton CreateSlimeChunkButton() 
        {
            SlimeChunkTool tool = new(ToolScene, new SlimeChunkChecker(Renderer.Scene.Domain));
            Renderer.AddPainter(tool);
            return CreateButton(tool, "SlimeChunks", "Slime chunk viewer");
        }
        private ToolButton CreateAxisButton() 
        {
            AxisTool tool = new(ToolScene);
            Renderer.AddPainter(tool);
            return CreateButton(tool, "CardinalAxis", "Cardinal (x; z) axes");
        }
        private ToolButton CreateChunkHighlightsButton()
        {
            HighlightChunkTool tool = new(ToolScene, RenderInvoker, Renderer.GraphicsCanvas);
            Renderer.AddPainter(tool);
            return CreateButton(tool, "HighlightChunk", "Chunk cursor highlighter");
        }
        private ToolButton CreateMeasureLengthButton()
        {
            MeasureLengthTool tool = new(ToolScene, RenderInvoker, Renderer.GraphicsCanvas);
            Renderer.AddPainter(tool);
            return CreateButton(tool, "Measure", "Measure length");
        }
        private ToolButton CreateDayNightCycleButton() 
        {
            DayNightCycleTool tool = new(Renderer.Scene);
            ToolButton output = new(tool)
            {
                ToolTip = "Night mode",
                Icon = ToolButtonIcons.Get("NightMode")
            };

            return output;
        }
        private ToolButton CreateGoToButton()
        {
            GoToTool tool = new(Renderer.Scene, Renderer.GraphicsCanvas);
            ToolButton output = new(tool)
            {
                ToolTip = "Go to position in world",
                Icon = ToolButtonIcons.Get("GoTo")
            };

            tool.Owner = output;

            return output;
        }
        private ToolButton CreateExportAsImageButton()
        {
            ExportAsImageTool tool = new(Renderer);
            ToolButton output = new(tool)
            {
                ToolTip = "Export as image",
                Icon = ToolButtonIcons.Get("Export")
            };

            return output;
        }
        private ToolButton CreateFilterButton()
        {
            FilterTool tool = new(Renderer.Scene);
            ToolButton output = new(tool)
            {
                Icon = ToolButtonIcons.Get("BlockFilter"),
                Name = "Block filter",
                ToolTip = "Filter blocks"
            };

            return output;
        }
        private ToolButton CreateRenderSettingsButton()
        {
            RenderSettingsTool tool = new(Renderer.Scene);
            ToolButton output = new(tool)
            {
                Icon = ToolButtonIcons.Get("Appearance"),
                Name = "Render settings",
                ToolTip = "Change render settings"
            };

            return output;
        }
        private ToolButton CreateBrowseButton()
        {
            BrowseTool tool = new(Renderer.Scene, MainWindow);
            ToolButton output = new(tool)
            {
                Icon = ToolButtonIcons.Get("OpenWorld"),
                Name = "Open world ",
                ToolTip = "Open a new world"
            };

            return output;
        }

        private ToolButton CreateButton(IToggleableTool tool, string iconKey, string toolTip)
        {
            ToolButton output = new(tool)
            {
                Tool = tool,
                ToolTip = toolTip,
                Icon = ToolButtonIcons.Get(iconKey)
            };

            AddInvoker(tool);

            return output;
        }
        private void AddInvoker(IToggleableTool tool) 
        {
            tool.OnTurnedOn += isTurnedOn => RenderInvoker.Render();
        }
    }
}
