using Mapper.Gui.Model;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    public class ToolButtonHook
    {
        public IToolButton ToolButton { get; set; }

        public MouseColorProperties TurnedOff { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(22, 22, 22)),
            MouseOver = new SolidColorBrush(Color.FromRgb(65, 65, 65)),
            MouseDown = new SolidColorBrush(Color.FromRgb(82, 82, 82))
        };
        public MouseColorProperties TurnedOn { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(82, 82, 82)),
            MouseOver = new SolidColorBrush(Color.FromRgb(98, 98, 98)),
            MouseDown = new SolidColorBrush(Color.FromRgb(115, 115, 115))
        };

        public MouseColorProperties DisabledTurnedOn { get; } = new MouseColorProperties()
        {
            Default = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            MouseOver = new SolidColorBrush(Color.FromRgb(98, 98, 98)),
            MouseDown = new SolidColorBrush(Color.FromRgb(115, 115, 115))
        };

        private enum MouseState
        {
            Default,
            Over,
            Down
        }

        public ToolButtonHook(IToolButton tool)
        {
            ToolButton = tool;

            StartHook();
            OnToolTurnedOn(ToolButton.Tool.IsTurnedOn);
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

            ToolButton.Button.Background = color;
        }
    }
}
