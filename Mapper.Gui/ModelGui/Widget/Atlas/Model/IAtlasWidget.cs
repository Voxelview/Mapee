using System;
using System.Collections.Generic;
using WorldEditor;

namespace Mapper.Gui.Model
{
    /// <summary>
    /// The world picker's model. Every member here is touched on the UI thread only - the scan
    /// runs on worker threads but marshals before it publishes anything, which is what keeps this
    /// whole surface lock-free.
    /// </summary>
    public interface IAtlasWidget
    {
        bool IsOpen { get; }

        /// <summary>
        /// Whether there is a map behind the picker. The scrim leans on this: over a loaded map it
        /// has terrain to suppress, over the empty checker it has nothing and a heavy scrim just
        /// makes the window look broken.
        /// </summary>
        bool HasLoadedWorld { get; }

        /// <summary>
        /// The directory of the world currently on the map, or null. Plates match on this rather
        /// than on the name, because two launchers' saves folders can hold two different worlds
        /// both called "New World".
        /// </summary>
        string? OpenWorldDirectory { get; }

        event EventHandler<bool>? OpenChanged;

        IReadOnlyList<IAtlasEntry> Entries { get; }

        /// <summary>The list was replaced wholesale - rebuild every plate.</summary>
        event EventHandler? EntriesReset;

        /// <summary>One entry finished loading, or failed. Update that plate alone.</summary>
        event EventHandler<IAtlasEntry>? EntryUpdated;
        event EventHandler<IAtlasEntry>? EntryRemoved;

        string SearchTerm { get; set; }
        event EventHandler? SearchChanged;

        /// <summary>
        /// Which dimension the plates are drawn from, and the one a picked world opens into.
        /// </summary>
        Dimension SelectedDimension { get; set; }
        event EventHandler<Dimension>? SelectedDimensionChanged;

        void Open();
        void Close();
        void Refresh();

        /// <summary>Loads the world and closes the picker. A pending entry is ignored.</summary>
        void Select(IAtlasEntry entry);

        /// <summary>Picks a level.dat from anywhere on disk, for worlds outside the saves folder.</summary>
        void BrowseExternal();
    }
}
