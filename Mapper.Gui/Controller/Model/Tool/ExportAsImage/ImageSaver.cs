using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Controller
{
    public class ImageSaver : IImageSaver
    {
        public FullResolutionImageArgs DefaultFullResArgs => new FullResolutionImageArgs()
        {
            ClipArea = true,
            CheckerPatternEnabled = true,
            // The checker never reads SolidColor - Draw only reaches it when the pattern is
            // off - so black here was a second, contradictory answer to the same question.
            // Transparent lets the struct round-trip back through the window unchanged.
            BackgroundColor = Color.FromArgb(0, 0, 0, 0)
        };

        public ImageScreenshotSaver ScreenshotSaver { get; set; }
        public ImageFullResolutionSaver ImageFullResolutionSaver { get; set; }

        public ImageSaver(Scene scene, Renderer renderer) 
        {
            ScreenshotSaver = new ImageScreenshotSaver(renderer.GraphicsCanvas);
            ImageFullResolutionSaver = new ImageFullResolutionSaver(scene, renderer);
        }

        public Size GetScreenshotSize()
        {
            return ScreenshotSaver.GetSize();
        }
        public Size GetFullResolutionSize(FullResolutionImageArgs args)
        {
            return ImageFullResolutionSaver.GetSize(args);
        }
        public ColorPair GetCheckerColors()
        {
            return ImageFullResolutionSaver.GetCheckerColors();
        }

        public void SaveAsScreenshot(string path)
        {
            ScreenshotSaver.SaveScreenshot(path);
        }
        public void SaveAsFullResolution(string path, FullResolutionImageArgs args)
        {
            ImageFullResolutionSaver.SaveAsFullResolutionImage(path, args);
        }
    }
}
