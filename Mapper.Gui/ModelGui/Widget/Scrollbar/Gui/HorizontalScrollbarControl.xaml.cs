using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for HorizontalScrollbarControl.xaml
    /// </summary>
    public partial class HorizontalScrollbarControl : UserControl
    {
        public IScrollbarWidget Scrollbar { get; }

        private bool _preventScrollBarUpdate = false;

        public HorizontalScrollbarControl(IScrollbarWidget scrollbar)
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

            // Hidden rather than Collapsed: the bar has to keep being arranged, because it
            // measures its thumb against its own arranged length, and a collapsed control
            // measures zero and could never work out that it is needed again.
            Visibility = needed ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
