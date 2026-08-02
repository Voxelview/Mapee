using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for VerticalScrollbarControl.xaml
    /// </summary>
    public partial class VerticalScrollbarControl : UserControl
    {
        public IScrollbarWidget Scrollbar { get; }

        private bool _preventScrollBarUpdate = false;

        public VerticalScrollbarControl(IScrollbarWidget scrollbar)
        {
            InitializeComponent();

            Scrollbar = scrollbar;
            Scrollbar.Update += Scrollbar_Update;
        }

        private void Scrollbar_Loaded(object sender, RoutedEventArgs e)
        {
            SetScrollbar();
        }
        private void Scrollbar_ValueChanged(object? sender, double value)
        {
            if (_preventScrollBarUpdate) return;

            _preventScrollBarUpdate = true;
            Scrollbar.SetLeftMostVisiblePoint(value);
            _preventScrollBarUpdate = false;
        }

        private void Scrollbar_Update(object? sender, EventArgs e)
        {
            _preventScrollBarUpdate = true;
            SetScrollbar();
            _preventScrollBarUpdate = false;
        }
        private void SetScrollbar()
        {
            bool needed = ScrollbarUtilities.AdjustScrollbar(ScrollbarControl, Scrollbar);

            // Hidden rather than Collapsed, for the same reason as the horizontal bar: the bar
            // measures its thumb against its own arranged length.
            Visibility = needed ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
