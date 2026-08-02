using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for StylePanel.xaml
    /// </summary>
    public partial class StylePanel : UserControl
    {
        public new IStyle Style { get; }

        private bool _isDown = false;
        private readonly Brush _backgroundBorder;
        private readonly Brush _mouseOverBorder;
        private readonly Brush _mouseDownBorder;

        public StylePanel(IStyle style)
        {
            InitializeComponent();

            Style = style;
            StyleLabel.Content = Style.Name;
            StyleIcon.Source = Style.Icon;

            _backgroundBorder = MainBorder.BorderBrush;

            _mouseOverBorder = new SolidColorBrush(Color.FromRgb(138, 138, 138));
            _mouseOverBorder.Freeze();

            _mouseDownBorder = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            _mouseDownBorder.Freeze();
        }

        /// <summary>
        /// 2 to match the dimension picker's ring - see DimensionButtonPanel.Select. It lands on
        /// the outer two pixels, which covers MainBorder's resting rule and stops short of the
        /// thumbnail, so a selected tile is a clean band of accent either way.
        /// </summary>
        public void Select()
        {
            IconBorder.BorderThickness = new Thickness(2);
        }
        public void Deselect()
        {
            IconBorder.BorderThickness = new Thickness();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isDown) MainBorder.BorderBrush = _mouseOverBorder;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            MainBorder.BorderBrush = _mouseDownBorder;
            _isDown = true;
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            MainBorder.BorderBrush = _mouseOverBorder;
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            MainBorder.BorderBrush = _backgroundBorder;
            _isDown = false;
        }

        private void StyleIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            ImageContainer.Background = Brushes.White;
        }
        private void StyleIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            ImageContainer.Background = Brushes.Transparent;
        }
        private void StyleIcon_MouseMove(object sender, MouseEventArgs e)
        {
            ImageContainer.Background = Brushes.White;
        }
    }
}
