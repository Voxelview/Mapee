using Mapper.Gui.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for RailControl.xaml
    /// </summary>
    public partial class RailControl : UserControl
    {
        /// <summary>
        /// Square, and large enough for the one glyph that overruns the 24 grid: a cube is
        /// taller than it is wide, so OpenWorld measures 18 x 20.784 where the rest are 18
        /// square. Anything under that clips it.
        /// </summary>
        /// <summary>
        /// Even, and it must stay even. The glyphs are 18 wide, so an odd button leaves a half
        /// pixel on each side for layout rounding to break one way, which shifts the icon off
        /// centre. 30 leaves exactly 6, and the rail's 40 leaves exactly 5 around the button.
        /// </summary>
        private const double BUTTON_SIZE = 30;

        /// <summary>
        /// The top button's glyph is drawn to this rather than to GlyphIcon's 18, so it reads as
        /// a peer of the 25-grid dimension art directly beneath it instead of as one of the
        /// tools below. Short of 25 because the cube is the one glyph that overruns its grid:
        /// even at 22 it still stands 25.4 tall, so matching the dimension tile's 25 exactly
        /// would leave it visibly the larger of the two. Even, for the same centring reason as
        /// <see cref="BUTTON_SIZE"/>.
        /// </summary>
        private const double PRIMARY_ICON_SIZE = 22;

        /// <summary>
        /// Clear space above and below each button, so adjacent fills never touch.
        /// </summary>
        private const double BUTTON_GAP = 3;

        /// <summary>
        /// The accent laid over a lit tool's glyph, at the same tone the outline around it takes
        /// - see ToolButtonHook.TurnedOn.DefaultBorder. Frozen because every button in the rail
        /// gets the same instance.
        /// </summary>
        private static readonly SolidColorBrush ICON_ACCENT = CreateAccentBrush();

        private static SolidColorBrush CreateAccentBrush()
        {
            SolidColorBrush output = new(Color.FromRgb(226, 218, 0));
            output.Freeze();

            return output;
        }

        public IToolbarWidget Toolbar { get; }

        public RailControl(IToolbarWidget toolbar)
        {
            InitializeComponent();

            Toolbar = toolbar;
        }

        /// <summary>
        /// Places a control below the top button and above the tools, divided from them. Used
        /// for the dimension button.
        /// </summary>
        public void SetHeader(UIElement header)
        {
            HeaderHost.Content = header;
            HeaderSeparator.Visibility = Visibility.Visible;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPrimaryButton();
            LoadSegments();
        }

        private void LoadPrimaryButton()
        {
            if (Toolbar.PrimaryButton is null) return;

            PrimaryHost.Content = CreateButton(Toolbar.PrimaryButton, PRIMARY_ICON_SIZE);
            _ = new ToolButtonHook(Toolbar.PrimaryButton);
        }

        /// <summary>
        /// One divider per segment boundary and none inside a segment - the inverse of what the
        /// horizontal toolbar did. Buttons that belong together simply sit together, and a rule
        /// appears only where the grouping actually changes.
        /// </summary>
        private void LoadSegments()
        {
            bool pushedToEnd = false;

            for (int i = 0; i < Toolbar.ToolButtonSegments.Count; i++)
            {
                IToolButtonSegment segment = Toolbar.ToolButtonSegments[i];

                if (segment.AlignToEnd && !pushedToEnd)
                {
                    // One greedy row takes everything left over, so this segment and any after
                    // it come to rest against the foot of the rail. No divider with it: the gap
                    // separates them far more plainly than a rule would.
                    ButtonContainer.RowDefinitions.Add(new RowDefinition()
                    {
                        Height = new GridLength(1, GridUnitType.Star)
                    });

                    pushedToEnd = true;
                }
                else if (i > 0)
                {
                    AddSeparator();
                }

                AddGap(segment.LeftGap);

                foreach (IToolButton button in segment.Tools)
                {
                    AddRow(CreateButton(button), GridLength.Auto);
                    _ = new ToolButtonHook(button);
                }

                AddGap(segment.RightGap);
            }
        }

        private void AddRow(UIElement element, GridLength height)
        {
            ButtonContainer.RowDefinitions.Add(new RowDefinition()
            {
                Height = height
            });

            Grid.SetColumn(element, 0);
            Grid.SetRow(element, ButtonContainer.RowDefinitions.Count - 1);
            ButtonContainer.Children.Add(element);
        }
        private void AddGap(double height)
        {
            if (height <= 0) return;

            ButtonContainer.RowDefinitions.Add(new RowDefinition()
            {
                Height = new GridLength(height, GridUnitType.Pixel)
            });
        }
        /// <summary>
        /// A fixed rule. It used to be tinted to follow the two tools it sat between, which
        /// meant toggling a tool visibly repainted the divider next to it - the grouping is a
        /// property of the rail, not of what happens to be switched on, so it holds still.
        /// </summary>
        private void AddSeparator()
        {
            Border separator = new()
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 51, 55)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 7, 0)
            };

            // Taller than the rule it carries, so the two groups get air between them. The
            // buttons on either side bring BUTTON_GAP of their own, hence the modest figure.
            AddRow(separator, new GridLength(7, GridUnitType.Pixel));
        }

        private Button CreateButton(IToolButton button, double? iconSize = null)
        {
            Button output = new()
            {
                Width = BUTTON_SIZE,
                Height = BUTTON_SIZE,

                // The button's fill IS its hover and toggle state, so stacked flush the states
                // of neighbouring buttons run into each other as one block. This keeps them
                // separate shapes.
                Margin = new Thickness(0, BUTTON_GAP, 0, BUTTON_GAP),

                Content = CreateImageControl(button, iconSize),
                ToolTip = button.ToolTip,
                Style = Resources["ToolButton"] as Style
            };

            button.SetButton(output);

            return output;
        }

        /// <summary>
        /// Icon only. A tool's Name is what the old horizontal toolbar drew beside the glyph on
        /// the three window-opening buttons; a rail this narrow has nowhere to put it, and the
        /// tooltip already says the same thing in more words.
        /// </summary>
        private UIElement CreateImageControl(IToolButton button, double? iconSize = null)
        {
            Image output = new()
            {
                Stretch = Stretch.Uniform
            };

            if (button.Icon is null)
            {
                output.Visibility = Visibility.Collapsed;
                return output;
            }

            output.Source = button.Icon;

            Size? size = iconSize is null
                ? GlyphIcon.Measure(button.Icon)
                : GlyphIcon.Measure(button.Icon, iconSize.Value);
            if (size is not null)
            {
                output.Width = size.Value.Width;
                output.Height = size.Value.Height;
            }

            return WithAccentOverlay(output, button.Icon);
        }

        /// <summary>
        /// Pairs the glyph with an accent-coloured copy of its own shape, left invisible until
        /// <see cref="ToolButtonHook"/> lights it for a tool that is switched on.
        /// </summary>
        /// <remarks>
        /// The overlay is masked by the glyph's geometry rather than being a second image,
        /// because there is only one white version of each icon and this needs to leave that one
        /// alone - every button in the rail shares it.
        /// <para>
        /// A DrawingBrush, not an ImageBrush: the glyphs are vector on a 24 grid drawn at 18, and
        /// their 1.333 strokes are meant to resolve to whole pixels. An ImageBrush would rasterize
        /// the drawing once and stretch that, which puts the mask fractionally off the image it
        /// has to sit exactly on top of. A DrawingBrush keeps the geometry and renders it at the
        /// same resolution the Image does.
        /// </para>
        /// </remarks>
        private static Grid WithAccentOverlay(Image glyph, ImageSource icon)
        {
            Rectangle overlay = new()
            {
                Width = glyph.Width,
                Height = glyph.Height,
                Fill = ICON_ACCENT,
                Opacity = 0,

                // The button owns the click; this only exists to be looked at.
                IsHitTestVisible = false,

                OpacityMask = icon is DrawingImage drawing
                    ? new DrawingBrush(drawing.Drawing) { Stretch = Stretch.Uniform }
                    : new ImageBrush(icon) { Stretch = Stretch.Uniform }
            };

            Grid host = new();
            host.Children.Add(glyph);
            host.Children.Add(overlay);

            return host;
        }

    }
}
