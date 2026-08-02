using AssetSystem.Block;
using MapScanner;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for BlockGroupingPanel.xaml
    /// </summary>
    public partial class BlockGroupingPanel : UserControl
    {
        public BlockEntry<BlockGrouping> BlockEntry { get; }

        public BlockGroupingPanel(BlockEntry<BlockGrouping> blockEntry)
        {
            InitializeComponent();

            BlockEntry = blockEntry;
            BlockEntryLabel.Content = BlockEntry.BlockName;

            SetMouseFeedback();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            SetMouseFeedback();
        }

        private void SetMouseFeedback()
        {
            Brush backroundBrush = Background;

            Brush mouseOverBackgroundBrush = new SolidColorBrush(Color.FromRgb(29, 31, 33));
            mouseOverBackgroundBrush.Freeze();

            Brush mouseDownBackgroundBrush = new SolidColorBrush(Color.FromRgb(41, 44, 48));
            mouseDownBackgroundBrush.Freeze();

            bool isDown = false;

            MouseMove += (sender, e) => { if (!isDown) Background = mouseOverBackgroundBrush; };
            MouseDown += (sender, e) =>
            {
                Background = mouseDownBackgroundBrush;
                isDown = true;
            };
            MouseUp += (sender, e) => Background = mouseOverBackgroundBrush;
            MouseLeave += (sender, e) =>
            {
                Background = backroundBrush;
                isDown = false;
            };
        }
    }
}
