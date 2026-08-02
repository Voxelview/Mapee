using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for MapViewer.xaml
    /// </summary>
    public partial class MapViewer : Window
    {
        public event EventHandler? Shown;

        public bool HasBeenShown { get; private set; }

        /// <summary>
        /// Held from <see cref="Initialize"/> until Loaded, because the frame template it goes
        /// into is only reachable through <see cref="CustomFrameWindowInitializer"/>, and that
        /// cannot be built before the template is applied.
        /// </summary>
        private FrameworkElement? _titleBarContent;

        public MapViewer()
        {
            InitializeComponent();
            SetSize();
        }

        public void Initialize(MapViewerArgs args)
        {
            CanvasContainer.Children.Add(args.Canvas);
            VerticalScrollBarContainer.Children.Add(args.VerticalScrollbar);
            HorizontalScrollBarContainer.Children.Add(args.HorizontalScrollbar);
            FooterGrid.Children.Add(args.Footer);
            RailContainer.Children.Add(args.Rail);

            _titleBarContent = args.TitleBarContent;

            foreach (Control widget in args.Widgets)
            {
                Panel.SetZIndex(widget, 100);

                // The rail owns column 0 now, so an unpositioned widget would land in it
                // rather than over the map. Pin every overlay to the canvas cell.
                Grid.SetRow(widget, 0);
                Grid.SetColumn(widget, 1);

                GlobalContainer.Children.Add(widget);
            }
        }

        private void SetSize()
        {
            Width = SystemParameters.PrimaryScreenWidth * (1250 / 1920F);
            Height = SystemParameters.PrimaryScreenHeight * (800 / 1080F);

            if (Width < 900 || Height < 700)
            {
                Width = SystemParameters.PrimaryScreenWidth * 0.8F;
                Height = SystemParameters.PrimaryScreenHeight * 0.9F;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CustomFrameWindowInitializer frameInitializer = new(this, Template);

            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(Environment.GetCommandLineArgs()[0]);
            Title = fileInfo.ProductName;
            frameInitializer.SetSecondaryTitle($"v{fileInfo.FileVersion}");

            if (_titleBarContent is not null) frameInitializer.SetTitleBarContent(_titleBarContent);

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (HasBeenShown) return;
            HasBeenShown = true;

            Shown?.Invoke(this, EventArgs.Empty);
        }
    }
}
