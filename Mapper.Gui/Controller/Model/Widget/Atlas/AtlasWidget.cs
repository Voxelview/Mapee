using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WorldEditor;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// The world picker's model - the world list, the background scan, open state and the hovered
    /// plate. AtlasControl is a view over this, the same split StylebarWidget and StylebarControl
    /// already use.
    ///
    /// Threading invariant, stated once because everything else depends on it: the entry list and
    /// every event here are touched on the UI thread ONLY. Scan workers touch locals and Post.
    /// That is what keeps this whole class lock-free.
    /// </summary>
    public class AtlasWidget : IAtlasWidget
    {
        public Scene Scene { get; }

        /// <summary>
        /// Still one folder, as it was in BrowseTool. Making this a list needs somewhere to store
        /// it, and the app has no user-settings layer.
        /// </summary>
        public string SavesDirectory { get; set; }

        public bool IsOpen { get; private set; }
        public bool HasLoadedWorld => Scene.Domain.CurrentWorld is not null;
        public string? OpenWorldDirectory => Scene.Domain.CurrentWorld?.Level.Directory;

        /// <summary>
        /// The three the app knows how to colour. A datapack dimension is still readable through
        /// the map itself; it just has no plate here.
        /// </summary>
        public static readonly Dimension[] Dimensions =
        {
            Dimension.Overworld,
            Dimension.Nether,
            Dimension.TheEnd
        };

        public Dimension SelectedDimension
        {
            get => _selectedDimension;
            set
            {
                if (_selectedDimension == value) return;

                _selectedDimension = value;
                SelectedDimensionChanged?.Invoke(this, value);
            }
        }

        public IReadOnlyList<IAtlasEntry> Entries => _entries;

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                string term = value ?? string.Empty;
                if (term == _searchTerm) return;

                _searchTerm = term;
                SearchChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler<bool>? OpenChanged;
        public event EventHandler? EntriesReset;
        public event EventHandler<IAtlasEntry>? EntryUpdated;
        public event EventHandler<IAtlasEntry>? EntryRemoved;
        public event EventHandler? SearchChanged;
        public event EventHandler<Dimension>? SelectedDimensionChanged;

        private readonly List<IAtlasEntry> _entries = new();
        private readonly Dispatcher _dispatcher;
        private readonly IObjectReader<string, Level?> _levelReader;
        private readonly ImageSource _defaultPlate;

        private CancellationTokenSource? _scan;
        private Dimension _selectedDimension = Dimension.Overworld;
        private string _searchTerm = string.Empty;

        public AtlasWidget(Scene scene, IObjectReader<string, Level?> levelReader)
        {
            Scene = scene;

            _levelReader = levelReader;
            _dispatcher = Dispatcher.CurrentDispatcher;

            SavesDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\.minecraft\\saves";

            // Built here, on the UI thread, because its URI is relative and resolves against the
            // Application - which is fragile from a scan worker. Frozen, so every worker can hand
            // out this one instance.
            _defaultPlate = CreateDefaultPlate();
        }

        // ---- open state -----------------------------------------------------------------

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            OpenChanged?.Invoke(this, true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;

            OpenChanged?.Invoke(this, false);
        }

        // ---- selection ------------------------------------------------------------------

        public void Select(IAtlasEntry entry)
        {
            // A pending plate has no Level behind it. The plate is also hit-test disabled while
            // pending, so this is the second lock on the same door rather than the only one.
            if (entry.State != AtlasEntryState.Ready || entry.Level is null) return;

            Close();
            Scene.SetWorld(entry.Level);

            // Opening a world always lands in the Overworld, so this is what makes picking a plate
            // while the Nether is selected actually take you there. ChangeDimension builds the
            // dimension on demand, so it is safe immediately after SetWorld and safe for a
            // dimension the world has never generated - that just renders empty.
            if (SelectedDimension != Dimension.Overworld) Scene.ChangeDimension(SelectedDimension);
        }

        public void BrowseExternal()
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "Dat files|*.dat;"
            };

            // != true, not "is null". ShowDialog returns a bool? that is never null, so the old
            // browser's null check let a cancelled dialog fall straight through - it only survived
            // because Path.GetDirectoryName("") happens to come back null.
            if (dialog.ShowDialog() != true) return;

            string? directory = Path.GetDirectoryName(dialog.FileName);
            if (directory is null) return;

            Level? level = TryReadLevel(directory);
            if (level is null) return;

            Close();
            Scene.SetWorld(level);
        }

        // ---- scanning -------------------------------------------------------------------

        public void Refresh()
        {
            _scan?.Cancel();
            _scan = new CancellationTokenSource();

            CancellationToken token = _scan.Token;
            string directory = SavesDirectory;

            Task.Run(() => Scan(directory, token), token);
        }

        private void Scan(string directory, CancellationToken token)
        {
            string[] worlds;
            try
            {
                worlds = Directory.Exists(directory) ? Directory.GetDirectories(directory) : Array.Empty<string>();
            }
            catch
            {
                worlds = Array.Empty<string>();
            }

            // Folder write time, not LastPlayed. LastPlayed lives inside level.dat and is not known
            // until the read this ordering has to come before - and the two agree in nearly every
            // case. A list that settles once beats a list that re-sorts itself out from under the
            // pointer.
            try
            {
                Array.Sort(worlds, (left, right) =>
                    Directory.GetLastWriteTimeUtc(right).CompareTo(Directory.GetLastWriteTimeUtc(left)));
            }
            catch
            {
                // A folder vanishing mid-sort is not worth losing the whole list over.
            }

            AtlasEntry[] pending = Array.ConvertAll(worlds, world => new AtlasEntry(world));

            Post(token, () =>
            {
                _entries.Clear();
                _entries.AddRange(pending);

                EntriesReset?.Invoke(this, EventArgs.Empty);
            });

            ParallelOptions options = new()
            {
                CancellationToken = token,

                // LevelReader allocates a decompression buffer per level.dat, so an unbounded
                // fan-out is an unbounded allocation spike. The old browser never capped this; it
                // got away with it by running once, blocking, on a folder you were already waiting
                // on.
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.ForEach(pending, options, entry => Load(entry, token));
            }
            catch (OperationCanceledException)
            {
                // A refresh landed on top of this one. The newer scan owns the list now.
            }
        }

        private void Load(AtlasEntry entry, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            Level? level = TryReadLevel(entry.Directory);
            if (level is null)
            {
                Post(token, () => Drop(entry));
                return;
            }

            Dictionary<Dimension, DimensionPlate> dimensions = new();
            ImageSource? icon;

            try
            {
                icon = LoadWorldIcon(entry.Directory);

                foreach (Dimension dimension in Dimensions)
                {
                    IReadOnlyCollection<Coords> footprint = RegionFootprintReader.Read(level, dimension);

                    // Null where nothing is stored, and left null: the art box then shows that
                    // dimension's own checker and nothing else, which says "never been here"
                    // honestly. Falling back to the world's icon would put an Overworld
                    // screenshot behind a Nether that does not exist.
                    dimensions[dimension] = new DimensionPlate(
                        footprint,
                        FootprintImageFactory.Create(footprint, level, dimension));
                }
            }
            catch
            {
                dimensions.Clear();
                icon = null;
            }

            Post(token, () =>
            {
                entry.SetLoaded(level, dimensions, icon);
                EntryUpdated?.Invoke(this, entry);
            });
        }

        private void Drop(AtlasEntry entry)
        {
            entry.SetFailed();

            if (_entries.Remove(entry)) EntryRemoved?.Invoke(this, entry);
        }

        private Level? TryReadLevel(string directory)
        {
            try
            {
                return _levelReader.Read($"{directory}\\level.dat");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// BeginInvoke, not Invoke. Invoke from inside a Parallel.ForEach body queues every worker
        /// behind the UI thread and turns the fan-out straight back into a queue. Background
        /// priority lets input and layout run between plates, so the grid fills in rather than
        /// arriving all at once.
        /// </summary>
        private void Post(CancellationToken token, Action action)
        {
            if (token.IsCancellationRequested) return;

            _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (token.IsCancellationRequested) return;
                action();
            });
        }

        // ---- plates ---------------------------------------------------------------------

        /// <summary>
        /// The world's own icon.png, frozen so it can cross back from the scan thread. Lifted from
        /// the browser this replaces, which already had it right.
        /// </summary>
        private static ImageSource? LoadWorldIcon(string directory)
        {
            string iconFile = $"{directory}\\icon.png";
            if (!File.Exists(iconFile)) return null;

            try
            {
                using FileStream imageStream = File.OpenRead(iconFile);

                BitmapImage icon = new();
                icon.BeginInit();
                icon.StreamSource = imageStream;
                icon.CacheOption = BitmapCacheOption.OnLoad;
                icon.EndInit();
                icon.Freeze();

                return icon;
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource CreateDefaultPlate()
        {
            BitmapImage output = new();
            output.BeginInit();

            // A pack URI, and absolute. The relative form works from XAML and from the
            // BitmapImage(Uri) constructor because both carry a BaseUri to resolve against; set
            // through UriSource inside a BeginInit block there is none, and WPF falls back to
            // treating it as a path relative to the working directory - which is how this asked
            // for C:\Resources\Image\Misc and took the app down on startup.
            output.UriSource = new Uri("pack://application:,,,/Resources/Image/Misc/DefaultWorld_128px.png");

            // OnLoad so the decode finishes here rather than lazily - a BitmapImage still loading
            // cannot be frozen, and unfrozen it could not be shared with the scan threads.
            output.CacheOption = BitmapCacheOption.OnLoad;
            output.EndInit();
            output.Freeze();

            return output;
        }
    }
}
