using CommonUtilities.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WorldEditor;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for WorldBrowser.xaml
    /// </summary>
    public partial class WorldBrowser : Window
    {
        public IList<string> BaseDirectories { get; private set; }
        public IObjectReader<string, Level?> LevelReader { get; private set; }

        public WorldPanelEntry Selected { get; private set; }
        public bool DialogClosed { get; private set; } = true;

        public event EventHandler? WorldSelected;

        private readonly IEnumerable<WorldPanelEntry> _entries;
        private readonly BitmapImage _default = new BitmapImage(new Uri("/Resources/Image/Misc/DefaultWorld_128px.png", UriKind.Relative));

        private bool _wasDown = false;

        public WorldBrowser(IList<string> baseDirectories, IObjectReader<string, Level?> levelReader)
        {
            InitializeComponent();

            BaseDirectories = baseDirectories;
            LevelReader = levelReader;

            _entries = BaseDirectories.SelectMany(LoadEntries).OrderByDescending(x => x.Level.LastPlayed).ToList();
            foreach (WorldPanelEntry entry in _entries)
            {
                AddWorld(entry);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ = new CustomFrameWindowInitializer(this, Template);
        }

        private IEnumerable<WorldPanelEntry> LoadEntries(string baseDirectory)
        {
            if(!Directory.Exists(baseDirectory)) return Enumerable.Empty<WorldPanelEntry>();

            string[] worlds = Directory.GetDirectories(baseDirectory);
            SynchronizedList<WorldPanelEntry> output = new(worlds.Length);

            Parallel.ForEach(worlds, world =>
            {
                if (!TryLoadEntry(world, out WorldPanelEntry entry)) return;
                output.Add(entry);
            });

            return output.OrderByDescending(x => x.Level.LastPlayed);
        }
        private bool TryLoadEntry(string world, out WorldPanelEntry entry)
        {
            try
            {
                Level? level = LevelReader.Read($"{world}\\level.dat");
                if (level is null) 
                {
                    entry = new WorldPanelEntry();
                    return false;
                }
                
                BitmapImage icon;

                // Bedrock worlds keep their thumbnail in world_icon.jpeg
                string iconFile = $"{world}\\icon.png";
                if (!File.Exists(iconFile)) iconFile = $"{world}\\world_icon.jpeg";

                if (File.Exists(iconFile))
                {
                    using (FileStream imageStream = File.OpenRead(iconFile))
                    {
                        icon = new BitmapImage();
                        icon.BeginInit();
                        icon.StreamSource = imageStream;
                        icon.CacheOption = BitmapCacheOption.OnLoad;
                        icon.EndInit();
                        icon.Freeze();
                    }
                }
                else
                {
                    icon = _default;
                }

                entry = new WorldPanelEntry(level, icon);
                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);

                entry = new WorldPanelEntry();
                return false;
            }
        }
        private void AddWorld(WorldPanelEntry entry)
        {
            WorldPanel panel = new(entry);

            panel.MouseDown += PanelMouseDown;
            panel.MouseUp += PanelMouseUp;

            if (WorldPanelContainer.Children.Count < 1)
            {
                panel.Height += 4;
            }

            WorldPanelContainer.RowDefinitions.Add(new RowDefinition()
            {
                Height = new GridLength(panel.Height)
            });

            Grid.SetRow(panel, WorldPanelContainer.RowDefinitions.Count - 1);
            Grid.SetColumn(panel, 0);
            WorldPanelContainer.Children.Add(panel);
        }

        private void PanelMouseDown(object sender, EventArgs e)
        {
            _wasDown = true;
        }
        private void PanelMouseUp(object sender, EventArgs e)
        {
            if (!_wasDown) return;

            Selected = ((WorldPanel)sender).Entry;
            WorldSelected?.Invoke(this, EventArgs.Empty);

            DialogClosed = false;
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "Dat files|*.dat;"
            };
            bool? result = dialog.ShowDialog();
            if (result is null) return;

            string? directory = Path.GetDirectoryName(dialog.FileName);
            if (directory is null || !TryLoadEntry(directory, out WorldPanelEntry entry)) return;

            Selected = entry;
            WorldSelected?.Invoke(this, EventArgs.Empty);

            DialogClosed = false;
            Close();
        }
    }
}
