using System.Collections.Generic;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui.Model
{
    /// <summary>
    /// One world in the Atlas. Everything past <see cref="DirectoryName"/> arrives asynchronously,
    /// which is why the nullables are here rather than the list being built all at once.
    /// </summary>
    public interface IAtlasEntry
    {
        string Directory { get; }

        /// <summary>
        /// Known from the start - it is the folder name, with no file read behind it. This is what
        /// a pending plate shows, and what search matches on before a world has been read.
        /// </summary>
        string DirectoryName { get; }

        AtlasEntryState State { get; }

        /// <summary>Null whenever <see cref="State"/> is not Ready.</summary>
        Level? Level { get; }

        /// <summary>
        /// Region coordinates for one dimension - one unit is 512 blocks. Empty for a dimension the
        /// world has never generated, and for worlds that predate region files entirely.
        /// </summary>
        IReadOnlyCollection<Coords> FootprintFor(Dimension dimension);

        /// <summary>
        /// The footprint drawing for one dimension, or null where there is nothing stored. Frozen,
        /// so it may be handed straight to an Image from any thread.
        /// </summary>
        ImageSource? PlateFor(Dimension dimension);

        /// <summary>
        /// The world's own icon.png, or null if it has none. Kept separate from
        /// <see cref="Plate"/> because it is now shown in its own right rather than only as the
        /// stand-in for a world whose footprint could not be read cheaply.
        /// </summary>
        ImageSource? Icon { get; }
    }
}
