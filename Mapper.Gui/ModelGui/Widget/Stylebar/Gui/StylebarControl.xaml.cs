using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for StylebarControl.xaml
    /// </summary>
    public partial class StylebarControl : UserControl
    {
        public IStylebarWidget Stylebar { get; }

        private IList<StylePanel> _styles = new List<StylePanel>();

        /// <summary>
        /// How tall a column of styles gets before the next one starts. The set is in a popup
        /// now rather than down the window's edge, so this is only about the popup's shape - it
        /// no longer decides how many styles you can see without a second click.
        /// </summary>
        private const int MAX_STYLES_IN_COLUMN = 5;

        public StylebarControl(IStylebarWidget stylebar)
        {
            InitializeComponent();

            Stylebar = stylebar;
            SetStylePanels();
            SetChip();

            Stylebar.StyleCollectionChanged += Stylebar_StyleCollectionChanged;
        }

        private void SetStylePanels()
        {
            StyleGrid.ColumnDefinitions.Clear();
            StyleGrid.Children.Clear();
            _styles.Clear();

            IReadOnlyList<IStyle> styles = Stylebar.Styles;

            for (int i = 0; i < styles.Count; i += MAX_STYLES_IN_COLUMN)
            {
                Grid column = new()
                {
                    Width = double.NaN,
                    Height = double.NaN,
                    Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                StyleGrid.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength(0, GridUnitType.Auto)
                });

                Grid.SetRow(column, 0);
                Grid.SetColumn(column, StyleGrid.ColumnDefinitions.Count - 1);
                StyleGrid.Children.Add(column);

                AddToStyleGrid(column, styles.Skip(i).Take(Math.Min(MAX_STYLES_IN_COLUMN, styles.Count - i)).ToList());
            }
        }
        private void AddToStyleGrid(Grid grid, IList<IStyle> styles)
        {
            grid.RowDefinitions.Clear();
            grid.Children.Clear();

            for (int i = 0; i < styles.Count; i++)
            {
                IStyle style = styles[i];

                StylePanel panel = new(style)
                {
                    Margin = new Thickness(0, i == 0 ? 0 : 6, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                panel.MouseDown += Style_MouseDown;

                grid.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(0, GridUnitType.Auto)
                });

                Grid.SetRow(panel, grid.RowDefinitions.Count - 1);
                Grid.SetColumn(panel, 0);
                grid.Children.Add(panel);

                _styles.Add(panel);
                if (Stylebar.SelectedStyleId == style.Id) panel.Select();
                else panel.Deselect();
            }
        }

        /// <summary>
        /// The chip wears the current style, so the collapsed state still answers "what am I
        /// looking at" without opening anything.
        /// </summary>
        private void SetChip()
        {
            foreach (IStyle style in Stylebar.Styles)
            {
                if (style.Id != Stylebar.SelectedStyleId) continue;

                ChipIcon.Source = style.Icon;
                ChipLabel.Text = style.Name;
                return;
            }
        }

        private void ChipButton_Click(object sender, RoutedEventArgs e)
        {
            StylePopup.IsOpen = !StylePopup.IsOpen;
        }

        private void Style_MouseDown(object? sender, EventArgs e)
        {
            if (sender is null || sender is not StylePanel panel) return;

            Stylebar.SelectedStyleId = panel.Style.Id;
            StylePopup.IsOpen = false;
        }
        private void Stylebar_StyleCollectionChanged(object? sender, IStyle style)
        {
            foreach (StylePanel panel in _styles)
            {
                if (panel.Style != style) panel.Deselect();
                else panel.Select();
            }

            SetChip();
        }
    }
}
