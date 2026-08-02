using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for StylebarControl.xaml
    /// </summary>
    public partial class StylebarControl : UserControl
    {
        public IStylebarWidget Stylebar { get; }

        private IList<StylePanel> _styles = new List<StylePanel>();
        private IList<StylePanel> _otherStyles = new List<StylePanel>();

        private bool _collapsed = true;

        private const int MAX_STYLES_IN_COLUMN = 5;

        public StylebarControl(IStylebarWidget stylebar)
        {
            InitializeComponent();

            Stylebar = stylebar;
            SetStylePanels();
            SetState(true);

            Stylebar.StyleCollectionChanged += Stylebar_StyleCollectionChanged;
        }

        private void SetStylePanels()
        {
            IList<IStyle> mainStyles = Stylebar.Styles.Take(MAX_STYLES_IN_COLUMN).ToList();

            SetMainStyleGrid(mainStyles);
            SetOtherStyleGrid(Stylebar.Styles.Skip(mainStyles.Count).ToList());
        }

        private void SetMainStyleGrid(IList<IStyle> styles) 
        {
            _styles.Clear();

            AddToStyleGrid(MainStyleGrid, styles, _styles);
        }
        private void SetOtherStyleGrid(IList<IStyle> styles) 
        {
            _otherStyles.Clear();
            StyleGrid.ColumnDefinitions.Clear();
            StyleGrid.Children.Clear();

            MoreButtonBorder.Visibility = styles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StyleGrid.Visibility = styles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (styles.Count == 0) return;

            for (int i = 0, index = 0; i < styles.Count; i += MAX_STYLES_IN_COLUMN, index++) 
            {
                Grid grid = new() 
                {
                    Width = double.NaN,
                    Height = double.NaN,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                StyleGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });

                Grid.SetRow(grid, 0);
                Grid.SetColumn(grid, StyleGrid.ColumnDefinitions.Count - 1);
                StyleGrid.Children.Add(grid);

                AddToStyleGrid(grid, styles.Skip(i).Take(Math.Min(MAX_STYLES_IN_COLUMN, styles.Count - i)).ToList(), _otherStyles);
            }

            _styles = _styles.Concat(_otherStyles).ToList();
        }
        private void AddToStyleGrid(Grid grid, IList<IStyle> styles, IList<StylePanel> output) 
        {
            grid.RowDefinitions.Clear();
            grid.Children.Clear();

            for (int i = 0; i < styles.Count; i++)
            {
                IStyle style = styles[i];

                StylePanel panel = new(style)
                {
                    Margin = new Thickness(0, 0, 0, i == styles.Count - 1 ? 6 : 7),
                    VerticalAlignment = VerticalAlignment.Top
                };
                panel.MouseDown += Style_MouseDown;

                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });

                Grid.SetRow(panel, grid.RowDefinitions.Count - 1);
                Grid.SetColumn(panel, 0);
                grid.Children.Add(panel);

                output.Add(panel);
                if (Stylebar.SelectedStyleId == style.Id) panel.Select();
                else panel.Deselect();
            }
        }

        private void Style_MouseDown(object? sender, EventArgs e) 
        {
            if (sender is null || sender is not StylePanel panel) return;
            Stylebar.SelectedStyleId = panel.Style.Id;
        }
        private void Stylebar_StyleCollectionChanged(object? sender, IStyle style) 
        {
            foreach (StylePanel panel in _styles) 
            {
                if (panel.Style != style) panel.Deselect();
                else panel.Select();
            }

            bool otherSelected = false;
            foreach (StylePanel panel in _otherStyles)
            {
                if (panel.Style != style) panel.Deselect();
                else {
                    panel.Select();
                    otherSelected = true;
                }
            }

            if (otherSelected) MoreButtonBorder.BorderBrush = Brushes.Gold;
            else MoreButtonBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(49, 49, 49));
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            SetState(!_collapsed);
        }
        private void SetState(bool state) 
        {
            _collapsed = state;

            if (_collapsed)
            {
                MoreButton.Content = "More";
                MoreButton.ToolTip = "Show more styles";
                StyleGrid.Visibility = Visibility.Collapsed;
                return;
            }

            MoreButton.Content = "Collapse";
            MoreButton.ToolTip = "Show less styles";
            StyleGrid.Visibility = Visibility.Visible;
        }
    }
}
