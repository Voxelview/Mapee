using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System.Collections.Generic;

namespace Mapper.Gui.Controller
{
    public class ToolbarWidget : IToolbarWidget
    {
        public IList<IToolButtonSegment> ToolButtonSegments { get; } = new List<IToolButtonSegment>();

        public IToolButton? PrimaryButton { get; }

        public MapViewer MainWindow { get; }
        public Renderer Renderer { get; }
        public ToolScene ToolScene { get; }
        public IRenderInvoker RenderInvoker { get; }
        public IAtlasWidget Atlas { get; }

        public ToolbarWidget(MapViewer mainWindow, Renderer renderer, IAtlasWidget atlas) 
        {
            MainWindow = mainWindow;
            Atlas = atlas;
            Renderer = renderer;
            ToolScene = new ToolScene(Renderer.Scene);
            RenderInvoker = new RenderInvoker(Renderer);

            // Opening a world is the one thing you do before anything else here works, so it
            // sits above the dimension button at the top of the rail rather than among the
            // tools, and is drawn at the dimension art's size instead of the glyph size.
            PrimaryButton = CreateBrowseButton();

            // Segments are the grouping mechanism: the rail draws one divider between segments
            // and none inside them, so buttons sit together by sharing a segment.

            // What you draw onto the map.
            ToolButtonSegment overlaySegment = new();
            overlaySegment.Tools.Add(CreateGridToolButton());
            overlaySegment.Tools.Add(CreateSlimeChunkButton());
            overlaySegment.Tools.Add(CreateAxisButton());
            overlaySegment.Tools.Add(CreateChunkHighlightsButton());
            overlaySegment.Tools.Add(CreateMeasureLengthButton());

            // What changes how the map itself comes out.
            ToolButtonSegment renderSegment = new();
            renderSegment.Tools.Add(CreateDayNightCycleButton());
            renderSegment.Tools.Add(CreateExportAsImageButton());

            // Settings, held at the foot of the rail - reached around the work rather than
            // during it, so they stay out of the run of tools you actually use on the map.
            ToolButtonSegment settingsSegment = new()
            {
                AlignToEnd = true
            };

            settingsSegment.Tools.Add(CreateFilterButton());
            settingsSegment.Tools.Add(CreateRenderSettingsButton());

            ToolButtonSegments.Add(overlaySegment);
            ToolButtonSegments.Add(renderSegment);
            ToolButtonSegments.Add(settingsSegment);
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
            BrowseTool tool = new(Atlas, MainWindow);
            ToolButton output = new(tool)
            {
                Icon = ToolButtonIcons.Get("OpenWorld"),
                Name = "Open world",
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
