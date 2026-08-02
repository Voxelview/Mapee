using Mapper.Gui.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for ToolbarControl.xaml
    /// </summary>
    public partial class ToolbarControl : UserControl
    {
        public IToolbarWidget Toolbar { get; }

        public ToolbarControl(IToolbarWidget toolbar)
        {
            InitializeComponent();

            Toolbar = toolbar;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            double width = LoadSegments();
            Width = width;
        }

        private double LoadSegments()
        {
            double width = 0;

            for (int i = 0; i < Toolbar.ToolButtonSegments.Count; i++)
            {
                IToolButtonSegment segment = Toolbar.ToolButtonSegments[i];

                width += AddGap(segment.LeftGap);
                width += LoadSegment(segment);
                width += AddGap(segment.RightGap);
            }

            return width;
        }
        private double LoadSegment(IToolButtonSegment segment) 
        {
            double width = 0;

            ToolButtonHook? prevHook = null;
            DockPanel? prevPanel = null;
            Border? prevSeparator = null;

            for (int i = 0; i < segment.Tools.Count; i++)
            {
                IToolButton button = segment.Tools[i];

                Button control = CreateButton(button);

                ButtonContainer.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength(0, GridUnitType.Auto)
                });

                Grid.SetRow(control, 0);
                Grid.SetColumn(control, ButtonContainer.ColumnDefinitions.Count - 1);
                ButtonContainer.Children.Add(control);

                Size size = new Size(double.PositiveInfinity, double.PositiveInfinity);
                control.Measure(size);
                control.Arrange(new Rect(control.DesiredSize));

                ToolButtonHook hook = new ToolButtonHook(button);
                if (prevHook is not null && prevPanel is not null && prevSeparator is not null)
                {
                    _ = new SeparatorHook(prevPanel, prevSeparator, prevHook, hook);
                }

                width += control.ActualWidth;
                if (i < segment.Tools.Count - 1)
                {
                    width += AddSeparator(out DockPanel panel, out Border separator);

                    prevPanel = panel;
                    prevSeparator = separator;
                }

                prevHook = hook;
            }

            return width;
        }
        private double AddGap(double width)
        {
            if (width <= 0) return 0;

            ButtonContainer.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(width, GridUnitType.Pixel)
            });

            return width;
        }
        private double AddSeparator(out DockPanel panel, out Border separator) 
        {
            panel = CreateSeparator(out separator);

            ButtonContainer.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(panel.Width, GridUnitType.Pixel)
            });

            Grid.SetRow(panel, 0);
            Grid.SetColumn(panel, ButtonContainer.ColumnDefinitions.Count - 1);
            ButtonContainer.Children.Add(panel);

            return panel.Width;
        }

        private Button CreateButton(IToolButton button) 
        {
            Button output = new() 
            {
                Width = double.NaN,
                Height = 33,
                Content = CreatePanel(button),
                ToolTip = button.ToolTip,
                Style = Resources["ToolButton"] as Style
            };

            button.SetButton(output);

            return output;
        }
        private StackPanel CreatePanel(IToolButton button) 
        {
            StackPanel output = new()
            {
                Width = double.NaN,
                Height = 33,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 6, 0)
            };

            Image image = CreateImageControl(button);
            TextBlock block = CreateTextBlockControl(button);

            output.Children.Add(image);
            output.Children.Add(block);

            if (image.Visibility == Visibility.Visible && block.Visibility == Visibility.Visible) 
            {
                image.Margin = new Thickness(image.Margin.Left + 2, image.Margin.Top, image.Margin.Right, image.Margin.Bottom);
            }

            return output;
        }
        private Image CreateImageControl(IToolButton button)
        {
            Image output = new()
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(5, 0, 0, 0)
            };

            if (button.Icon is null)
            {
                output.Visibility = Visibility.Collapsed;
                return output;
            }

            output.Source = button.Icon;

            Size? size = GlyphIcon.Measure(button.Icon);
            if (size is not null)
            {
                output.Width = size.Value.Width;
                output.Height = size.Value.Height;
            }

            return output;
        }
        private TextBlock CreateTextBlockControl(IToolButton button)
        {
            TextBlock textBlock = new() 
            {
                Foreground = new SolidColorBrush(Color.FromRgb(239, 239, 239)),
                FontSize = 15,
                Margin = new Thickness(7, 7, 3, 0)
            };

            if (button.Name is not null)
            {
                textBlock.Text = button.Name;
            }
            else
            {
                textBlock.Visibility = Visibility.Collapsed;
                return textBlock;
            }

            return textBlock;
        }

        private DockPanel CreateSeparator(out Border border) 
        {
            DockPanel output = new DockPanel() 
            {
                Background = Brushes.Transparent,
                Width = 1
            };

            border = new Border()
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(112, 112, 112)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Margin = new Thickness(0, 7, 0, 7)
            };

            output.Children.Add(border);

            return output;
        }
    }
}
