using Mapper.Gui.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mapper.Gui
{
    public class ToolButtonHook
    {
        public IToolButton ToolButton { get; set; }

        // Rest is the rail's own panel colour, so an untouched button is invisible chrome.
        // Everything off rest is the accent, and the accent is the only hue in the chrome now
        // that the panels are near-neutral - so it may as well come out of the map's own art.
        // It is the sun from the overworld dimension tile, #FFDC5A, where the ramp used to take
        // the grass from the same tile.
        //
        // Every tone is fully saturated - blue is 0 all the way down the ramp - and the ladder is
        // set in L* so the states stay evenly spaced apart. Zero blue is what does the work at
        // the dark end: these fills used to carry 40-60% saturation, which at low value is not a
        // dim yellow but an olive, and no amount of brightening fixes that. Draining the blue
        // turns the same tones amber.
        //
        // 83 is the ceiling here, not a choice - it is the brightest L* hue 47 reaches with no
        // blue in it. Anything above means letting blue back in, which trades vividness for
        // lightness, so the top of the ramp is spaced tighter than the bottom on purpose.
        //
        // The one to know is TurnedOn's DefaultBorder, (227, 178, 0): that is "selected" in this
        // UI, and the dimension and style pickers ring their selection in the very same colour
        // (DimensionButtonPanel.xaml, StylePanel.xaml). ToolButton.xaml carries the TurnedOff
        // hover and press pair for the buttons no hook owns - zoom, position, dimension. All
        // three files move together or the accent stops agreeing with itself.
        public MouseColorProperties TurnedOff { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(15, 16, 18)),
            MouseOver = new SolidColorBrush(Color.FromRgb(85, 66, 0)),
            MouseDown = new SolidColorBrush(Color.FromRgb(114, 89, 0)),

            DefaultBorder = new SolidColorBrush(Colors.Transparent),
            MouseOverBorder = new SolidColorBrush(Color.FromRgb(172, 135, 0)),
            MouseDownBorder = new SolidColorBrush(Color.FromRgb(210, 165, 0)),

            // Nothing at rest - an unlit tool is meant to disappear into the rail.
            DefaultTint = 0,
            MouseOverTint = 0.25,
            MouseDownTint = 0.35
        };
        public MouseColorProperties TurnedOn { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(104, 82, 0)),
            MouseOver = new SolidColorBrush(Color.FromRgb(128, 101, 0)),
            MouseDown = new SolidColorBrush(Color.FromRgb(153, 119, 0)),

            DefaultBorder = new SolidColorBrush(Color.FromRgb(227, 178, 0)),
            MouseOverBorder = new SolidColorBrush(Color.FromRgb(242, 190, 0)),
            MouseDownBorder = new SolidColorBrush(Color.FromRgb(255, 199, 0)),

            // Starts where the unlit ramp ends and carries on from there, so hovering a lit tool
            // still reads as a step up rather than landing back where an unlit one already was.
            DefaultTint = 0.45,
            MouseOverTint = 0.55,
            MouseDownTint = 0.65
        };

        // Latched but unusable: the same hue held well down, so it still reads as on without
        // competing with a tool you can actually press.
        public MouseColorProperties DisabledTurnedOn { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(71, 56, 0)),
            MouseOver = new SolidColorBrush(Color.FromRgb(128, 101, 0)),
            MouseDown = new SolidColorBrush(Color.FromRgb(153, 119, 0)),

            DefaultBorder = new SolidColorBrush(Color.FromRgb(135, 105, 0)),
            MouseOverBorder = new SolidColorBrush(Color.FromRgb(242, 190, 0)),
            MouseDownBorder = new SolidColorBrush(Color.FromRgb(255, 199, 0)),

            // A third of what a usable tool gets, which is what ToolButtonImage dims the glyph
            // to. Left at full strength the overlay would come out brighter than the icon it is
            // sitting on, and a tool you cannot press would draw the eye hardest of all. Flat
            // across the three: WPF raises no mouse events on a disabled control, so the hover
            // and press values here only exist to keep the struct honest.
            DefaultTint = 0.15,
            MouseOverTint = 0.15,
            MouseDownTint = 0.15
        };

        private enum MouseState
        {
            Default,
            Over,
            Down
        }

        /// <summary>
        /// The accent laid over the glyph, built by RailControl.WithAccentOverlay. Resolved once:
        /// the button's content is assembled before the hook is attached and never rebuilt, and
        /// SetBackground runs on every mouse event.
        /// </summary>
        private readonly Rectangle? _iconOverlay;

        public ToolButtonHook(IToolButton tool)
        {
            ToolButton = tool;
            _iconOverlay = FindIconOverlay();

            StartHook();
            OnToolTurnedOn(ToolButton.Tool.IsTurnedOn);
        }

        private Rectangle? FindIconOverlay()
        {
            if (ToolButton.Button?.Content is not Panel host) return null;

            foreach (UIElement child in host.Children)
            {
                if (child is Rectangle overlay) return overlay;
            }

            return null;
        }

        private void StartHook()
        {
            ToolButton.Tool.OnTurnedOn += OnToolTurnedOn;
            ToolButton.Tool.OnEnabled += OnEnabled;

            if (ToolButton.Button is null) return;

            ToolButton.Button.IsEnabled = ToolButton.Tool.Enabled;
            ToolButton.Button.Click += (sender, e) => ToolButton.Tool.IsTurnedOn = !ToolButton.Tool.IsTurnedOn;
            ToolButton.Button.PreviewMouseDown += (sender, e) => SetBackground(MouseState.Down, GetColorProperties());
            ToolButton.Button.PreviewMouseUp += (sender, e) => SetBackground(MouseState.Over, GetColorProperties());
            ToolButton.Button.MouseLeave += (sender, e) => SetBackground(MouseState.Default, GetColorProperties());
            ToolButton.Button.MouseEnter += (sender, e) => SetBackground(MouseState.Over, GetColorProperties());
        }

        private void OnToolTurnedOn(bool isOn)
        {
            SetBackground(GetCurrentMouseState(), GetColorProperties());
        }
        private void OnEnabled(bool isEnabled)
        {
            if (ToolButton.Button is null) return;

            SetBackground(GetCurrentMouseState(), GetColorProperties());
            ToolButton.Button.IsEnabled = isEnabled;
        }

        private MouseState GetCurrentMouseState()
        {
            if (ToolButton.Button is null || !ToolButton.Button.IsMouseOver) return MouseState.Default;

            bool leftButtonPressed = Mouse.LeftButton == MouseButtonState.Pressed;
            return leftButtonPressed ? MouseState.Down : MouseState.Over;
        }
        private MouseColorProperties GetColorProperties()
        {
            if (ToolButton.Tool.Enabled) return ToolButton.Tool.IsTurnedOn ? TurnedOn : TurnedOff;
            return ToolButton.Tool.IsTurnedOn ? DisabledTurnedOn : TurnedOff;
        }

        private void SetBackground(MouseState mouseState, MouseColorProperties properties)
        {
            if (ToolButton.Button is null) return;

            SolidColorBrush color = mouseState switch
            {
                MouseState.Default => properties.Default,
                MouseState.Over => properties.MouseOver,
                _ => properties.MouseDown,
            };

            SolidColorBrush? border = mouseState switch
            {
                MouseState.Default => properties.DefaultBorder,
                MouseState.Over => properties.MouseOverBorder,
                _ => properties.MouseDownBorder,
            };

            double tint = mouseState switch
            {
                MouseState.Default => properties.DefaultTint,
                MouseState.Over => properties.MouseOverTint,
                _ => properties.MouseDownTint,
            };

            ToolButton.Button.Background = color;
            ToolButton.Button.BorderBrush = border ?? Brushes.Transparent;

            if (_iconOverlay is not null) _iconOverlay.Opacity = tint;
        }
    }
}
