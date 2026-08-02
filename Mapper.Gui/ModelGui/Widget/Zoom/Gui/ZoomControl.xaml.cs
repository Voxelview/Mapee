using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for ZoomControl.xaml
    /// </summary>
    public partial class ZoomControl : UserControl
    {
        public IZoomWidget Zoom { get; }

        /// <summary>
        /// Set while the slider is being moved into agreement with the map. Both directions run
        /// through the same pair of events - moving the slider zooms the map, and the map
        /// zooming moves the slider - so without this the two chase each other.
        /// </summary>
        private bool _syncing = false;

        public ZoomControl(IZoomWidget zoom)
        {
            InitializeComponent();

            Zoom = zoom;
            Zoom.LevelChanged += Zoom_LevelChanged;

            _syncing = true;
            ZoomSlider.Minimum = Zoom.MinLevel;
            ZoomSlider.Maximum = Zoom.MaxLevel;
            ZoomSlider.Value = Zoom.Level;
            _syncing = false;

            SetLabel(Zoom.ZoomPercentage);
        }

        private void Zoom_LevelChanged(object? sender, EventArgs e)
        {
            _syncing = true;
            ZoomSlider.Value = Zoom.Level;
            _syncing = false;

            SetLabel(Zoom.ZoomPercentage);
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing) return;

            Zoom.SetLevel((int)e.NewValue);
        }

        private void DecreaseZoomButton_Click(object sender, RoutedEventArgs e)
        {
            Zoom.ZoomOut();
        }
        private void IncreaseZoomButton_Click(object sender, RoutedEventArgs e)
        {
            Zoom.ZoomIn();
        }

        private void SetLabel(double percentage)
        {
            percentage = Math.Round(percentage, 4);

            if (percentage > 1)
            {
                ZoomPercentageLabel.Content = $"{percentage.ToString("N1").Replace(",", ".")}x";
            }
            else
            {
                ZoomPercentageLabel.Content = $"{(int)(percentage * 100)}%";
            }
        }
    }
}
