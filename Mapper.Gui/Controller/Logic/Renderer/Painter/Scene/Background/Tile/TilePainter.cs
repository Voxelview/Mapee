using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Media;

namespace Mapper.Gui.Logic
{
    public class TilePainter : IPainter<BackgroundPaintArgs>
    {
        public DrawingGroup? DrawingGroup { get; set; }
        
        public ICheckerImageGenerator CheckerImageGenerator { get; set; }

        private ImageSource? _prevImage;
        private ColorPair _prevColor;

        public TilePainter() 
        {
            CheckerImageGenerator = new CheckerImageGenerator();
        }

        public void Paint(DrawingContext drawingContext, BackgroundPaintArgs args)
        {
            if (DrawingGroup is null) return;

            ImageSource image = GetImage(args);
            int xCount = (int)Math.Ceiling(args.BackgroundArea.Width / image.Width);
            int yCount = (int)Math.Ceiling(args.BackgroundArea.Height / image.Height);

            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    drawingContext.DrawImage(image, new Rect(x * image.Width + args.BackgroundArea.TopLeft.X, y * image.Height + args.BackgroundArea.TopLeft.Y, image.Width, image.Height));
                }
            }
        }
        private ImageSource GetImage(BackgroundPaintArgs args) 
        {
            if (_prevImage is null || _prevColor != args.Background.CheckedColorPair) 
            {
                const int CHECKER_SIZE = 14, TILES_COUNT = 40;
                Size size = new(CHECKER_SIZE * TILES_COUNT, CHECKER_SIZE * TILES_COUNT);

                _prevImage = CheckerImageGenerator.Generate(size, CHECKER_SIZE, args.Background.CheckedColorPair);
            }

            _prevColor = args.Background.CheckedColorPair;
            return _prevImage;
        }
    }
}
