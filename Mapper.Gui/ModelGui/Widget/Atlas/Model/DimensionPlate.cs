using System.Collections.Generic;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui.Model
{
    /// <summary>
    /// What the Atlas knows about one world in one dimension: the regions it has stored and the
    /// picture drawn from them.
    /// </summary>
    /// <param name="Footprint">
    /// Region coordinates, one unit per 512 blocks. Empty for a dimension the world has never
    /// generated.
    /// </param>
    /// <param name="Image">
    /// Null when the footprint is empty, which is the plate's signal to show that dimension's
    /// checker and nothing over it.
    /// </param>
    public readonly record struct DimensionPlate(IReadOnlyCollection<Coords> Footprint, ImageSource? Image);
}
