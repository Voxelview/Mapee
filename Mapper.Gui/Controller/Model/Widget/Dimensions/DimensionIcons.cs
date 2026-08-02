using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// The dimension buttons' art, looked up by the key it carries in DimensionIcons.xaml. Kept
    /// apart from <see cref="ToolButtonIcons"/> because the two sets share nothing: these are
    /// 25px pixel-art scenes drawn at their own size, those are 24-grid line glyphs sized by
    /// <see cref="GlyphIcon"/>.
    /// </summary>
    public static class DimensionIcons
    {
        private static readonly ResourceDictionary ICONS = new()
        {
            Source = new Uri("/Resources/Icon/DimensionIcons.xaml", UriKind.Relative)
        };

        public static ImageSource Get(string key)
        {
            return (ImageSource)ICONS[key];
        }
    }
}
