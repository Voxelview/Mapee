using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using System.IO;

namespace Mapper.Gui
{
    public class CustomFrameWindowInitializer
    {
        public Image MaximizeImage { get; }
        public Image NormalizeImage { get; }

        public Window Window { get; }
        public ControlTemplate Template { get; }
        public Panel Frame { get; }
        public Image WindowIcon { get; }
        public Button MinimizeButton { get; }
        public Button MaximizeButton { get; }
        public Button CloseButton { get; }

        private bool _mouseDownOnFrame = false, _inDrag = false;

        public CustomFrameWindowInitializer(Window window, ControlTemplate template)
        {
            Window = window;
            Template = template;

            object frameObj = template.FindName("FramePanel", window);
            if (frameObj is null || frameObj is not Panel framePanel)
            {
                throw new NullReferenceException();
            }
            Frame = framePanel;

            object windowIconObj = template.FindName("WindowIcon", window);
            if (windowIconObj is null || windowIconObj is not Image windowIcon)
            {
                throw new NullReferenceException();
            }
            WindowIcon = windowIcon;
            ApplyWindowIconRules();

            object minimizeButtonObj = template.FindName("MinimizeButton", window);
            if (minimizeButtonObj is null || minimizeButtonObj is not Button minimizeButton)
            {
                throw new NullReferenceException();
            }

            MinimizeButton = minimizeButton;

            object maximizeButtonObj = template.FindName("MaximizeButton", window);
            if (maximizeButtonObj is null || maximizeButtonObj is not Button maximizeButton)
            {
                throw new NullReferenceException();
            }
            MaximizeButton = maximizeButton;
            MaximizeButton.Visibility = Window.ResizeMode == ResizeMode.CanResize || Window.ResizeMode == ResizeMode.CanResizeWithGrip ? Visibility.Visible : Visibility.Collapsed;

            object closeButtonObj = template.FindName("CloseButton", window);
            if (closeButtonObj is null || closeButtonObj is not Button closeButton)
            {
                throw new NullReferenceException();
            }
            CloseButton = closeButton;

            if (MaximizeButton.Content is null || MaximizeButton.Content is not Image maximizeButtonImage)
            {
                throw new NullReferenceException();
            }
            MaximizeImage = maximizeButtonImage;

            NormalizeImage = new Image
            {
                Source = new BitmapImage(new Uri("/Resources/Image/WindowFrame/Normalize_16px.png", UriKind.Relative)),
                Stretch = Stretch.None,
                Margin = new Thickness(0, 0, 0, 2)
            };

            Window.StateChanged += WindowStateChanged;
            Window.MouseMove += DrawWhenMove;
            Window.MouseUp += (sender, e) =>
            {
                _inDrag = false;
            };

            Frame.MouseDown += FrameMouseDown;
            Frame.MouseUp += FrameMouseUp;
            Frame.MouseMove += FrameMouseMove;

            MinimizeButton.Click += MinimizeButtonClick;
            MaximizeButton.Click += MaximizeButtonClick;
            CloseButton.Click += CloseButtonClick;
        }

        /// <summary>
        /// Sizes the title bar's icon the way the toolbar sizes the button that opens the window,
        /// so the two show the same glyph at the same weight. The frame's slot is 18 square, and
        /// fitting a glyph that overruns its grid into a square shrinks it to about 0.56 and takes
        /// its strokes back under a pixel.
        /// </summary>
        private void ApplyWindowIconRules()
        {
            // Window.Icon rather than the image's own Source: the two are the same picture, but
            // the image reaches it through a template binding and this runs while the template is
            // still being walked. The window's property is already set either way.
            Size? size = GlyphIcon.Measure(Window.Icon);
            if (size is null) return;

            WindowIcon.Width = size.Value.Width;
            WindowIcon.Height = size.Value.Height;
        }

        public void SetSecondaryTitle(string secondaryTitle)
        {
            Label secondaryTitleLabel = (Label)Template.FindName("SecondaryTitle", Window);
            secondaryTitleLabel.Content = secondaryTitle;
        }

        private void WindowStateChanged(object? sender, EventArgs e)
        {
            if (Window.WindowState == WindowState.Maximized)
            {
                Window.BorderThickness = new Thickness(7);
                MaximizeButton.Content = NormalizeImage;
            }
            else
            {
                Window.BorderThickness = new Thickness(1);
                MaximizeButton.Content = MaximizeImage;
            }
        }

        private void FrameMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (Window.WindowState != WindowState.Maximized)
            {
                Window.DragMove();
            }
            else
            {
                _mouseDownOnFrame = true;
            }
        }
        private void FrameMouseUp(object sender, MouseButtonEventArgs e)
        {
            _mouseDownOnFrame = false;
        }
        private void FrameMouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseDownOnFrame) return;

            MinimizeFromFull();
            _mouseDownOnFrame = false;
        }

        private void MinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            Window.WindowState = WindowState.Minimized;
        }
        private void MaximizeButtonClick(object sender, RoutedEventArgs e)
        {
            if (Window.WindowState == WindowState.Normal)
            {
                Window.WindowState = WindowState.Maximized;
            }
            else
            {
                Window.WindowState = WindowState.Normal;
            }
        }
        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Window.Close();
        }

        private void MinimizeFromFull()
        {
            _inDrag = true;

            Window.Top = 0;
            Window.Left = GetNextXPosition();
            Window.WindowState = WindowState.Normal;
        }
        private int GetNextXPosition()
        {
            int x = (int)Mouse.GetPosition(Window).X;
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int originalWidth = (int)Window.RestoreBounds.Width;

            int leftBound = x - originalWidth / 2, rightBound = x + originalWidth / 2;
            if (leftBound < 0)
            {
                x = 0;
            }
            else if (rightBound > screenWidth)
            {
                x = screenWidth - originalWidth;
            }
            else
            {
                x -= originalWidth / 2;
            }

            return x;
        }

        private void DrawWhenMove(object sender, MouseEventArgs e)
        {
            if (_inDrag && e.LeftButton == MouseButtonState.Pressed) Window.DragMove();
        }
    }
}
