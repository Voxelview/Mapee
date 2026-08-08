using Mapper.Gui.Model;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for InformationControl.xaml
    /// </summary>
    public partial class InformationControl : UserControl
    {
        public IInformationWidget Information { get; }

        private TimedUpdater _timedInfoUpdater = new TimedUpdater();
        private TimedUpdater _timedCursorUpdater = new TimedUpdater();

        private SceneInformation? _lastInformation;
        private XzPoint _lastCursorPoint;

        private DispatcherTimer? _catchUpTimer;

        public InformationControl(IInformationWidget information)
        {
            InitializeComponent();

            Information = information;
            Information.InformationUpdate += Information_InformationUpdate;
            Information.CursorUpdate += Information_CursorUpdate;

            // Last, not first. This used to run before Information was assigned, and the tick can
            // reach SetCursorLabels, which dereferences Information.MainControl - latent only
            // because PreviousSkipped starts false.
            InitializeBackgrounWork();
        }

        private void InitializeBackgrounWork()
        {
            // Was a 50ms BackgroundWork thread, for the reason PositionControl already gives for
            // doing it this way instead: everything here is UI state, and on the thread the
            // interesting decision - PreviousSkipped - was being read off the UI thread while only
            // the paint was marshalled.
            _catchUpTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            _catchUpTimer.Tick += CatchUp_Tick;
            _catchUpTimer.Start();
        }

        /// <summary>
        /// Replays whatever the throttle dropped, so the bar always settles on the latest value
        /// rather than on the last one that happened to fall outside the 100ms window.
        /// </summary>
        private void CatchUp_Tick(object? sender, EventArgs e)
        {
            if (!IsVisible) return;

            // Through Update, not around it. TimedUpdater only clears PreviousSkipped when the
            // action actually runs, so calling the setters directly - as this did - left the flag
            // latched after the first skipped frame and repainted five labels every 50ms forever.
            if (_timedInfoUpdater.PreviousSkipped) _timedInfoUpdater.Update(Repaint);
            if (_timedCursorUpdater.PreviousSkipped) _timedCursorUpdater.Update(() => SetCursorLabels(_lastCursorPoint));
        }

        private void Information_InformationUpdate(object? sender, SceneInformation information)
        {
            _lastInformation = information;
            _timedInfoUpdater.Update(Repaint);
        }
        private void Information_CursorUpdate(object? sender, XzPoint point)
        {
            _lastCursorPoint = point;
            _timedCursorUpdater.Update(() => SetCursorLabels(point));
        }

        /// <summary>
        /// The one path into the labels, so the live feed and the catch-up tick cannot disagree
        /// about what the bar is showing.
        /// </summary>
        private void Repaint()
        {
            if (_lastInformation is not null) SetInformationLabels(_lastInformation);
        }

        private void SetInformationLabels(SceneInformation information)
        {
            LoadedRegionsLabel.Text = information.LoadedRegions.ToString();

            if (information.LoadedArea is not null)
            {
                string text;
                if (information.LoadedRegions == 0) text = "Empty";
                else text = SizeToString(information.LoadedArea.Value.Size);

                LoadedSizeLabel.Text = text;
            }

            VisibleAreaTopLeftLabel.Text = information.VisibleArea.TopLeftPoint.ToString("N0");
            VisibleAreaBottomRightLabel.Text = information.VisibleArea.BottomRightPoint.ToString("N0");
            VisibleAreaSizeLabel.Text = SizeToString(information.VisibleArea.Size);

            if (information.WorldInformation is not null)
            {
                WorldTextBlock.Visibility = Visibility.Visible;

                WorldNameLabel.Text = information.WorldInformation.WorldName;
                WorldVersionLabel.Text = information.WorldInformation.Version.VersionName;
            }
            else
            {
                // Collapsed rather than Hidden: the cell is a bordered box now, and a hidden one
                // would still hold its space as a gap the size of a world name.
                WorldTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private static string SizeToString(XzPoint size)
        {
            return $"{(int)size.X}x{(int)size.Z}";
        }

        private void SetCursorLabels(XzPoint point)
        {
            if (Information.MainControl is null || !Information.MainControl.IsVisible) return;

            SetCursorSegmentVisibility(Information.MainControl.IsMouseOver);
            if (!Information.MainControl.IsMouseOver) return;

            CursorOverBlockLabel.Text = point.ToString("N0");
            CursorOverChunkLabel.Text = BlockToChunk(point).ToString("N0");
        }

        /// <summary>
        /// Off the map there is no cursor position to report, so the cursor cell says None and
        /// the chunk cell - which would otherwise sit there empty and boxed - goes away.
        /// </summary>
        private void SetCursorSegmentVisibility(bool visible)
        {
            CursorOverBlockLabel.Text = string.Empty;
            CursorOverChunkLabel.Text = string.Empty;

            CursorHiddenLabel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            CursorOverBlockLabel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ChunkCell.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        private static XzPoint BlockToChunk(XzPoint block)
        {
            int x = (int)block.X / 16, z = (int)block.Z / 16;

            if (block.X < 0) x--;
            if (block.Z < 0) z--;

            return new XzPoint(x, z);
        }
    }
}
