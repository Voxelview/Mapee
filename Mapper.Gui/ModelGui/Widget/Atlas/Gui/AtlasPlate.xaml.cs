using Mapper.Gui.Model;
using System;
using WorldEditor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for AtlasPlate.xaml
    /// </summary>
    public partial class AtlasPlate : UserControl
    {
        public IAtlasEntry Entry { get; }

        public AtlasPlate(IAtlasEntry entry, string? openDirectory, Dimension dimension)
        {
            InitializeComponent();

            Entry = entry;
            Refresh(openDirectory, dimension);
        }

        /// <summary>
        /// Called again when the entry's scan lands, so a plate goes from its folder name to its
        /// world in place rather than being rebuilt and losing the pointer that is resting on it.
        /// </summary>
        public void Refresh(string? openDirectory, Dimension dimension)
        {
            ArtBorder.Background = DimensionCheckerBrush.For(dimension);
            PlateImage.Source = Entry.PlateFor(dimension);

            if (Entry.State != AtlasEntryState.Ready || Entry.Level is null)
            {
                // The folder name is free - no file behind it - so a pending plate still says
                // which world it is, at full size and in its final position.
                NameLabel.Text = Entry.DirectoryName;
                NameLabel.Foreground = Brushes.Gray;

                SubLabel.Text = "Reading…";
                SetFacts(string.Empty, string.Empty, string.Empty);

                IconFrame.Visibility = Visibility.Collapsed;
                OpenTag.Visibility = Visibility.Collapsed;

                // No Level to open, so the plate must not be clickable - and the cursor must not
                // promise that it is.
                IsHitTestVisible = false;
                Cursor = null;
                return;
            }

            NameLabel.Text = string.IsNullOrWhiteSpace(Entry.Level.WorldName)
                ? Entry.DirectoryName
                : Entry.Level.WorldName;

            NameLabel.SetResourceReference(ForegroundProperty, "LabelInformationDarkColor");

            SubLabel.Text = $"{Entry.DirectoryName}  ·  {FormatPlayed(Entry.Level.LastPlayed)}";

            SetFacts(
                Entry.Level.IsHardcode ? "Hardcore" : Entry.Level.GameType.ToString(),
                Entry.Level.AllowCommands ? "  ·  Cheats" : string.Empty,
                Entry.Level.Version.VersionName);

            // Hardcore is the one fact here worth noticing, so it recolours the run it is already
            // in rather than earning a badge of its own.
            ModeRun.SetResourceReference(TextElement.ForegroundProperty,
                Entry.Level.IsHardcode ? "LabelWarningColor" : "LabelInformationDarkColor");

            IconImage.Source = Entry.Icon;
            IconFrame.Visibility = Entry.Icon is not null ? Visibility.Visible : Visibility.Collapsed;

            OpenTag.Visibility = IsOpenWorld(openDirectory) ? Visibility.Visible : Visibility.Collapsed;

            IsHitTestVisible = true;
            Cursor = Cursors.Hand;
        }

        private void SetFacts(string mode, string cheats, string version)
        {
            ModeRun.Text = mode;
            CheatsRun.Text = cheats;
            VersionRun.Text = version;

            // The separator only earns its place when there is something on both sides of it.
            SeparatorRun.Text = mode.Length > 0 && version.Length > 0 ? "  ·  " : string.Empty;
        }

        private static string FormatPlayed(DateTime played)
        {
            return played == default ? "Never played" : played.ToString("dd.MM.yyyy");
        }

        /// <summary>
        /// Matched on the directory rather than the name, because two launchers' saves folders can
        /// hold two different worlds both called "New World".
        /// </summary>
        private bool IsOpenWorld(string? openDirectory)
        {
            if (openDirectory is null || Entry.Level?.Directory is null) return false;

            return string.Equals(openDirectory, Entry.Level.Directory, StringComparison.OrdinalIgnoreCase);
        }
    }
}
