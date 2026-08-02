using Mapper.Gui.Model;
using System.Collections.Generic;

namespace Mapper.Gui.Controller
{
    public class ToolButtonSegment : IToolButtonSegment
    {
        public IList<IToolButton> Tools { get; } = new List<IToolButton>();

        public double LeftGap { get; set; }
        public double RightGap { get; set; }

        public bool AlignToEnd { get; set; }
    }
}
