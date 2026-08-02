using System.Collections.Generic;

namespace Mapper.Gui.Model
{
    public interface IToolbarWidget
    {
        public IList<IToolButtonSegment> ToolButtonSegments { get; }

        /// <summary>
        /// The tool that sits apart from the segments, at the very top of the rail. It is drawn
        /// larger than the rest, so it is not one of them.
        /// </summary>
        public IToolButton? PrimaryButton { get; }
    }
}
