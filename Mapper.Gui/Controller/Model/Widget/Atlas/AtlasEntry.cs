using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// One world in the Atlas. Constructed knowing only its folder - which costs no IO - and
    /// filled in when the scan reaches it, so the grid can be on screen before anything has been
    /// read.
    /// </summary>
    public class AtlasEntry : IAtlasEntry
    {
        public string Directory { get; }
        public string DirectoryName { get; }

        public AtlasEntryState State { get; private set; } = AtlasEntryState.Pending;

        public Level? Level { get; private set; }
        public ImageSource? Icon { get; private set; }

        /// <summary>
        /// Every dimension is scanned up front - three directory listings against one level.dat
        /// read - so switching the picker's dimension is a redraw rather than a rescan.
        /// </summary>
        private readonly Dictionary<Dimension, DimensionPlate> _dimensions = new();

        public AtlasEntry(string directory)
        {
            Directory = directory;
            DirectoryName = Path.GetFileName(directory);
        }

        /// <summary>
        /// UI thread only, like everything else the widget publishes. The scan posts this rather
        /// than calling it from a worker.
        /// </summary>
        public void SetLoaded(Level level, IReadOnlyDictionary<Dimension, DimensionPlate> dimensions, ImageSource? icon)
        {
            Level = level;
            Icon = icon;

            _dimensions.Clear();
            foreach (KeyValuePair<Dimension, DimensionPlate> pair in dimensions) _dimensions[pair.Key] = pair.Value;

            State = AtlasEntryState.Ready;
        }

        public IReadOnlyCollection<Coords> FootprintFor(Dimension dimension)
        {
            return _dimensions.TryGetValue(dimension, out DimensionPlate plate)
                ? plate.Footprint
                : Array.Empty<Coords>();
        }

        public ImageSource? PlateFor(Dimension dimension)
        {
            return _dimensions.TryGetValue(dimension, out DimensionPlate plate) ? plate.Image : null;
        }

        public void SetFailed()
        {
            State = AtlasEntryState.Failed;
        }

    }
}
