using System.Windows;

namespace Mapper.Gui.Model
{
    public interface IImageSaver
    {
        FullResolutionImageArgs DefaultFullResArgs { get; }

        Size GetScreenshotSize();
        Size GetFullResolutionSize(FullResolutionImageArgs args);

        /// <summary>
        /// The checker pair the current dimension exports with, so the window can show it
        /// rather than name it. Overworld, the Nether and the End each have their own.
        /// </summary>
        ColorPair GetCheckerColors();

        void SaveAsScreenshot(string path);
        void SaveAsFullResolution(string path, FullResolutionImageArgs args);
    }
}
