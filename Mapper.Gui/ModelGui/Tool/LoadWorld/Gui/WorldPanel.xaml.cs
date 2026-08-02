using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for WorldPanel.xaml
    /// </summary>
    public partial class WorldPanel : UserControl
    {
        public WorldPanelEntry Entry { get; }

        private bool _isDown = false;
        private readonly Brush _redBrush;
        private readonly Brush _background;
        private readonly Brush _mouseOver;
        private readonly Brush _mouseDown;

        public WorldPanel(WorldPanelEntry entry)
        {
            InitializeComponent();

            _redBrush = new SolidColorBrush(Color.FromRgb(233, 0, 0));
            _redBrush.Freeze();

            _background = Background;

            _mouseOver = new SolidColorBrush(Color.FromRgb(29, 31, 33));
            _mouseOver.Freeze();

            _mouseDown = new SolidColorBrush(Color.FromRgb(39, 42, 44));
            _mouseDown.Freeze();

            Entry = entry;

            SetContent();
        }

        private void SetContent()
        {
            WorldIcon.Source = Entry.Icon;
            IngameNameLabel.Text = Entry.Level.WorldName;

            DateTime lastPlayed = Entry.Level.LastPlayed;
            DirectoryNameLabel.Text = $"{Path.GetFileName(Entry.Level.Directory)} ({lastPlayed:dd.MM.yyyy HH:mm})";

            if (!Entry.Level.IsHardcode)
            {
                GameTypeLabel.Text = $"{Entry.Level.GameType} Mode";
            }
            else
            {
                GameTypeLabel.Text = "Hardcore Mode";
                GameTypeLabel.Foreground = _redBrush;
                GameTypeLabel.FontWeight = FontWeights.SemiBold;
            }

            if (Entry.Level.AllowCommands)
            {
                CheatsLabel.Text = "Cheats";
            }
            else
            {
                CheatsCommaLabel.Text = string.Empty;
            }

            VersionLabel.Text = Entry.Level.Version.VersionName;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isDown) Background = _mouseOver;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            Background = _mouseDown;
            _isDown = true;
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            Background = _mouseOver;
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            Background = _background;
            _isDown = false;
        }
    }
}
