using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WorldEditor;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for AtlasControl.xaml
    /// </summary>
    public partial class AtlasControl : UserControl
    {
        private enum SortMode { Recent, Name, Version }

        /// <summary>
        /// Attached rather than a field per option, so the three sort labels can share one Style
        /// and light their own selected state without three named triggers.
        /// </summary>
        public static readonly DependencyProperty IsSelectedSortProperty =
            DependencyProperty.RegisterAttached("IsSelectedSort", typeof(bool), typeof(AtlasControl),
                new PropertyMetadata(false));

        public static bool GetIsSelectedSort(DependencyObject element) => (bool)element.GetValue(IsSelectedSortProperty);
        public static void SetIsSelectedSort(DependencyObject element, bool value) => element.SetValue(IsSelectedSortProperty, value);

        public IAtlasWidget Atlas { get; }

        // Just under the darkest panel in the app, so the picker reads as the map dimmed rather
        // than as a sheet laid over it. Two strengths because there are two things behind it: over
        // an empty checker there is nothing to suppress and a heavy scrim only makes the window
        // look broken, while over a loaded map the terrain is bright enough that plate labels lose
        // against anything lighter.
        private static readonly Brush SCRIM_OVER_MAP = CreateFrozen(Color.FromArgb(0xE0, 9, 10, 11));
        private static readonly Brush SCRIM_OVER_EMPTY = CreateFrozen(Color.FromArgb(0x66, 9, 10, 11));

        /// <summary>
        /// One plate's footprint in the wrap: AtlasPlate is 168 wide with a 7 margin either side.
        /// The two have to agree, so a change there is a change here.
        /// </summary>
        private const double PLATE_SLOT = 168 + 7 + 7;

        /// <summary>
        /// Always reserved, never measured. Letting the scroll bar come and go would change the
        /// usable width, which would change the column count, which would move every plate - and it
        /// appears and disappears exactly when a search changes how many plates there are.
        /// </summary>
        private const double SCROLLBAR = 10;

        private readonly Dictionary<IAtlasEntry, AtlasPlate> _plates = new();
        private readonly DoubleAnimation _fadeIn;
        private readonly DoubleAnimation _fadeOut;

        private SortMode _sort = SortMode.Recent;

        public AtlasControl(IAtlasWidget atlas)
        {
            InitializeComponent();

            Atlas = atlas;

            // Short, and eased, because this covers the whole map: linear at this size reads as a
            // flicker and anything longer than about a tenth of a second is in the way of somebody
            // who just wants to pick a world.
            _fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            _fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            // Collapsed only once the fade has finished, or the panel would vanish on frame one
            // and animate nothing. Attached once here rather than per close, which would stack a
            // handler every time the picker was opened.
            _fadeOut.Completed += (sender, e) =>
            {
                if (!Atlas.IsOpen) Visibility = Visibility.Collapsed;
            };

            // Built first, then subscribed. Safe against the scan because every update it posts
            // arrives as a queued dispatcher item and this constructor is one work item - nothing
            // can interleave. That would not hold if the widget marshalled with Invoke.
            Rebuild();
            ApplySortSelection();
            ApplyDimensionSelection();

            Atlas.OpenChanged += Atlas_OpenChanged;
            Atlas.EntriesReset += Atlas_EntriesReset;
            Atlas.EntryUpdated += Atlas_EntryUpdated;
            Atlas.EntryRemoved += Atlas_EntryRemoved;
            Atlas.SelectedDimensionChanged += Atlas_SelectedDimensionChanged;
        }

        // ---- open / close ---------------------------------------------------------------

        private void Atlas_OpenChanged(object? sender, bool isOpen)
        {
            if (isOpen)
            {
                Scrim.Background = Atlas.HasLoadedWorld ? SCRIM_OVER_MAP : SCRIM_OVER_EMPTY;

                Visibility = Visibility.Visible;
                BeginAnimation(OpacityProperty, _fadeIn);
                return;
            }

            BeginAnimation(OpacityProperty, _fadeOut);
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible) return;

            // Focused here rather than from the open handler: on the very first open the control
            // has not been laid out yet and Keyboard.Focus would be dropped. Focus is also what
            // makes OnKeyDown below fire at all - without it the arrow keys go straight to
            // ScaleBehaviour and pan the map behind the picker.
            Keyboard.Focus(SearchBox);
            SearchBox.SelectAll();
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            Atlas.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Atlas.Close();
                e.Handled = true;
                return;
            }

            // ScaleBehaviour hooks the WINDOW's KeyDown and pans on the arrows and resets zoom on
            // Ctrl+R, reading Keyboard.IsKeyDown rather than the event - so arrowing through the
            // search box would otherwise drag the map about behind the picker. Its handler is
            // attached with a plain +=, which does not see handled events, so marking them here is
            // enough to stop it.
            if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right) e.Handled = true;
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) e.Handled = true;
        }

        /// <summary>
        /// Snaps the panel to a whole number of plate columns, never more than there are worlds.
        /// Both halves matter: sizing to the window alone left the close button hanging past the
        /// last plate whenever the row was not full, and sizing to the visible plates moved the
        /// whole header every time a search matched fewer worlds. The total is stable under
        /// filtering, so this is.
        /// </summary>
        private void UpdateContentWidth()
        {
            double available = Scrim.ActualWidth - ContentHost.Margin.Left - ContentHost.Margin.Right;
            if (available <= 0) return;

            int fits = Math.Max(1, (int)((available - SCROLLBAR) / PLATE_SLOT));
            int columns = Math.Max(1, Math.Min(fits, _plates.Count));

            ContentHost.Width = columns * PLATE_SLOT + SCROLLBAR;

            // The header spans the scroll viewer, which is a scroll bar wider than the plates. Pull
            // its right edge back over the last column so the close button lines up with the last
            // plate instead of hanging off the end of the row.
            HeaderRow.Margin = new Thickness(7, 0, 7 + SCROLLBAR, 12);
        }

        private void Scrim_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) UpdateContentWidth();
        }

        // ---- list -----------------------------------------------------------------------

        private void Atlas_EntriesReset(object? sender, EventArgs e)
        {
            Rebuild();
        }

        private void Atlas_EntryUpdated(object? sender, IAtlasEntry entry)
        {
            // Refreshed in place rather than rebuilt, so a plate the pointer is resting on does
            // not vanish and reappear under it when its scan lands.
            if (_plates.TryGetValue(entry, out AtlasPlate? plate)) plate.Refresh(Atlas.OpenWorldDirectory, Atlas.SelectedDimension);

            // A world that has just been read can sort somewhere else - it now has a name, a
            // version and a real last-played date where it had only a folder.
            Reorder();
            ApplyFilter();
        }

        private void Atlas_EntryRemoved(object? sender, IAtlasEntry entry)
        {
            if (!_plates.TryGetValue(entry, out AtlasPlate? plate)) return;

            PlateContainer.Children.Remove(plate);
            _plates.Remove(entry);

            UpdateContentWidth();
            ApplyFilter();
        }

        private void Rebuild()
        {
            PlateContainer.Children.Clear();
            _plates.Clear();

            string? openDirectory = Atlas.OpenWorldDirectory;

            foreach (IAtlasEntry entry in Atlas.Entries)
            {
                AtlasPlate plate = new(entry, openDirectory, Atlas.SelectedDimension);
                plate.MouseLeftButtonUp += Plate_MouseLeftButtonUp;

                _plates[entry] = plate;
            }

            Reorder();
            UpdateContentWidth();
            ApplyFilter();
        }

        private void Plate_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Atlas.Select(((AtlasPlate)sender).Entry);
        }

        private void Atlas_SelectedDimensionChanged(object? sender, Dimension dimension)
        {
            // Redrawn in place, not rebuilt: every dimension was scanned up front, so this is
            // swapping an image and a background brush on plates that already exist.
            foreach (AtlasPlate plate in _plates.Values) plate.Refresh(Atlas.OpenWorldDirectory, dimension);

            ApplyDimensionSelection();
        }

        private void Dimension_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement option || option.Tag is not string tag) return;

            Atlas.SelectedDimension = tag switch
            {
                "nether" => Dimension.Nether,
                "end" => Dimension.TheEnd,
                _ => Dimension.Overworld
            };
        }

        private void ApplyDimensionSelection()
        {
            SetIsSelectedSort(DimensionOverworld, Atlas.SelectedDimension == Dimension.Overworld);
            SetIsSelectedSort(DimensionNether, Atlas.SelectedDimension == Dimension.Nether);
            SetIsSelectedSort(DimensionEnd, Atlas.SelectedDimension == Dimension.TheEnd);
        }

        // ---- sorting --------------------------------------------------------------------

        private void Sort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement option || option.Tag is not string tag) return;

            _sort = tag switch
            {
                "name" => SortMode.Name,
                "version" => SortMode.Version,
                _ => SortMode.Recent
            };

            ApplySortSelection();
            Reorder();
        }

        private void ApplySortSelection()
        {
            SetIsSelectedSort(SortRecent, _sort == SortMode.Recent);
            SetIsSelectedSort(SortName, _sort == SortMode.Name);
            SetIsSelectedSort(SortVersion, _sort == SortMode.Version);
        }

        /// <summary>
        /// Re-adds every plate in order. A WrapPanel lays its children out in child order and has
        /// no sort of its own, and at a few hundred plates clearing and re-adding costs one layout
        /// pass - cheaper than the bookkeeping needed to move them individually.
        /// </summary>
        private void Reorder()
        {
            List<AtlasPlate> ordered = new(_plates.Values);
            ordered.Sort(Compare);

            PlateContainer.Children.Clear();
            foreach (AtlasPlate plate in ordered) PlateContainer.Children.Add(plate);
        }

        private int Compare(AtlasPlate left, AtlasPlate right)
        {
            // A world still being read has no name, no version and no date, so it sorts last under
            // every mode rather than jumping up the grid when its scan lands.
            bool leftReady = left.Entry.State == AtlasEntryState.Ready;
            bool rightReady = right.Entry.State == AtlasEntryState.Ready;

            if (leftReady != rightReady) return leftReady ? -1 : 1;
            if (!leftReady) return 0;

            int result = _sort switch
            {
                SortMode.Name => string.Compare(Name(left), Name(right), StringComparison.CurrentCultureIgnoreCase),
                SortMode.Version => CompareVersion(left, right),
                _ => CompareRecent(left, right)
            };

            // Folder name as the tie-break, so two worlds that agree on the sorted field do not
            // swap places between one reorder and the next.
            return result != 0
                ? result
                : string.Compare(left.Entry.DirectoryName, right.Entry.DirectoryName, StringComparison.CurrentCultureIgnoreCase);
        }

        private static string Name(AtlasPlate plate)
        {
            string? name = plate.Entry.Level?.WorldName;
            return string.IsNullOrWhiteSpace(name) ? plate.Entry.DirectoryName : name;
        }

        private static int CompareRecent(AtlasPlate left, AtlasPlate right)
        {
            return Nullable.Compare(right.Entry.Level?.LastPlayed, left.Entry.Level?.LastPlayed);
        }

        /// <summary>
        /// By DataVersion, not by the printed name: "1.21.10" sorts before "1.21.9" as text, and
        /// snapshots do not order against releases at all.
        /// </summary>
        private static int CompareVersion(AtlasPlate left, AtlasPlate right)
        {
            int leftVersion = (int)(left.Entry.Level?.Version.Version ?? 0);
            int rightVersion = (int)(right.Entry.Level?.Version.Version ?? 0);

            return rightVersion.CompareTo(leftVersion);
        }

        // ---- search ---------------------------------------------------------------------

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = SearchBox.Text.Length > 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            Atlas.SearchTerm = SearchBox.Text;
            ApplyFilter();
        }

        /// <summary>
        /// Collapsed, not removed. A WrapPanel skips collapsed children in measure and arrange, so
        /// filtering reflows the grid for free and nothing has to be rebuilt or re-ordered.
        /// </summary>
        private void ApplyFilter()
        {
            string needle = Atlas.SearchTerm.Trim();
            int shown = 0;

            foreach (KeyValuePair<IAtlasEntry, AtlasPlate> pair in _plates)
            {
                bool matches = needle.Length < 1 || Matches(pair.Key, needle);

                pair.Value.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                if (matches) shown++;
            }

            UpdateCount(shown);
            UpdateEmptyState(shown, needle);
        }

        private static bool Matches(IAtlasEntry entry, string needle)
        {
            if (entry.DirectoryName.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;

            string? name = entry.Level?.WorldName;
            return name is not null && name.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateCount(int shown)
        {
            int total = _plates.Count;

            CountLabel.Text = shown == total
                ? total == 1 ? "1 world" : $"{total} worlds"
                : $"{shown} of {total}";
        }

        private void UpdateEmptyState(int shown, string needle)
        {
            if (shown > 0)
            {
                EmptyLabel.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyLabel.Text = needle.Length > 0
                ? $"Nothing here matches “{needle}”."
                : "No worlds in this folder.\nUse Browse to open one from somewhere else.";

            EmptyLabel.Visibility = Visibility.Visible;
        }

        // ---- actions --------------------------------------------------------------------

        private void Browse_Click(object sender, MouseButtonEventArgs e)
        {
            Atlas.BrowseExternal();
        }

        private static Brush CreateFrozen(Color color)
        {
            SolidColorBrush output = new(color);
            output.Freeze();

            return output;
        }
    }
}
