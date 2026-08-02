using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for PositionControl.xaml
    /// </summary>
    public partial class PositionControl : UserControl
    {
        public IPositionWidget Position { get; }

        private readonly TimedUpdater _timedUpdater = new TimedUpdater();
        private readonly DispatcherTimer _catchUpTimer;

        public PositionControl(IPositionWidget position)
        {
            InitializeComponent();

            Position = position;
            Position.CenterChanged += Position_CenterChanged;

            PositionIcon.Source = Position.Icon;

            Size? size = GlyphIcon.Measure(Position.Icon);
            if (size is not null)
            {
                PositionIcon.Width = size.Value.Width;
                PositionIcon.Height = size.Value.Height;
            }

            // The throttle drops updates rather than queueing them, so the last move of a pan is
            // usually one of the dropped ones and the readout would settle on a stale point.
            // This re-applies it once the gesture goes quiet. A DispatcherTimer rather than the
            // background thread the footer uses: everything here is UI state, and this way it is
            // only ever touched from the UI thread.
            _catchUpTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _catchUpTimer.Tick += CatchUp_Tick;
            _catchUpTimer.Start();

            SetLabel();
        }

        private void Position_CenterChanged(object? sender, EventArgs e)
        {
            _timedUpdater.Update(SetLabel);
        }

        private void CatchUp_Tick(object? sender, EventArgs e)
        {
            if (!_timedUpdater.PreviousSkipped) return;
            _timedUpdater.Update(SetLabel);
        }

        private void SetLabel()
        {
            PositionLabel.Text = Position.CenterPoint.ToString("N0");
        }

        private void PositionButton_Click(object sender, RoutedEventArgs e)
        {
            Position.OpenGoTo(PositionButton);
        }
    }
}
