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

            foreach (Control widget in args.Widgets)
            {
                Panel.SetZIndex(widget, 100);
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
            // Use the numeric parts: the FileVersion string is four-part (1.4.0.0), and the title shows v1.4.0.
            string version = $"v{fileInfo.FileMajorPart}.{fileInfo.FileMinorPart}.{fileInfo.FileBuildPart}";
            if (DataPacks.LoadedRevision > 0) version += $" · data r{DataPacks.LoadedRevision}";
            frameInitializer.SetSecondaryTitle(version);

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
