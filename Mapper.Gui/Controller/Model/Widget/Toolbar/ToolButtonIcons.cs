using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// The toolbar's glyphs, looked up by the key they carry in ToolButtonIcons.xaml. The
    /// dictionary is parsed once on first use, so buttons naming the same key share one drawing.
    /// </summary>
    public static class ToolButtonIcons
    {
        private static readonly ResourceDictionary ICONS = new()
        {
            Source = new Uri("/Resources/Icon/ToolButtonIcons.xaml", UriKind.Relative)
        };

        public static ImageSource Get(string key)
        {
            return (ImageSource)ICONS[key];
        }
    }
}
