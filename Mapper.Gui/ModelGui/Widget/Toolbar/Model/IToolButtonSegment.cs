using System.Collections.Generic;

namespace Mapper.Gui.Model
{
    public interface IToolButtonSegment
    {
        public IList<IToolButton> Tools { get; }
        public double LeftGap { get; }
        public double RightGap { get; }

        /// <summary>
        /// Pushes this segment, and everything after it, to the far end of the rail rather than
        /// letting it follow on from the segment above. For the tools you reach for around the
        /// work rather than during it.
        /// </summary>
        public bool AlignToEnd { get; }
    }
}
