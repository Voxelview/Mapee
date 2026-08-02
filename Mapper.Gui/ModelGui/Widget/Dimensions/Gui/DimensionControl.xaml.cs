using Mapper.Gui.Model;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorldEditor;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for DimensionControl.xaml
    /// </summary>
    public partial class DimensionControl : UserControl
    {
        public IDimensionWidget Dimensions { get; }

        /// <summary>
        /// What the style chip keeps between itself and the map's left edge. The dimension
        /// flyout matches it so the two open on one line.
        /// </summary>
        private const double MAP_EDGE_GAP = 7;

        private List<DimensionButtonPanel> _dimensions = new List<DimensionButtonPanel>();
        private DimensionButtonPanel? _extraDimensionButton = null;
        private DimensionUI? _extraDimension = null;

        public DimensionControl(IDimensionWidget dimensions)
        {
            InitializeComponent();

            Dimensions = dimensions;
            Dimensions.DimensionUpdate += Dimension_DimensionUpdate;
            Dimensions.DimensionSelectionChanged += Dimension_DimensionSelectionChanged;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetDimensions();
        }

        private void SetDimensions()
        {
            DimensionContainer.RowDefinitions.Clear();
            DimensionContainer.Children.Clear();
            _dimensions.Clear();

            _extraDimensionButton = null;
            _extraDimension = null;

            foreach (DimensionUI dimension in Dimensions.Dimensions)
            {
                DimensionButtonPanel dimensionButton = CreateDimensionControl(dimension);

                dimensionButton.MouseDown += Dimension_MouseDown;
                if (dimension.Dimension == Dimensions.CurrentDimension)
                {
                    dimensionButton.Select();
                }
            }

            if (Dimensions.ExtraDimensions.Count > 0)
            {
                _extraDimension = new DimensionUI(new Dimension("", "custom dimensions"), new BitmapImage(new Uri("/Resources/Image/Dimension/ExtraDimensions_32px.png", UriKind.Relative)));

                _extraDimensionButton = CreateDimensionControl(_extraDimension);
                if (IsCustomDimensionSelected()) _extraDimensionButton.Select();

                _extraDimensionButton.MouseDown += ExtraDimension_MouseDown;
            }

            SetCurrentDimensionIcon();
        }
        private bool IsCustomDimensionSelected()
        {
            foreach (Dimension dimension in Dimensions.ExtraDimensions)
            {
                if(dimension == Dimensions.CurrentDimension) return true;
            }

            return false;
        }

        private DimensionButtonPanel CreateDimensionControl(DimensionUI dimension)
        {
            DimensionButtonPanel button = new DimensionButtonPanel(dimension)
            {
                Margin = new Thickness(0, DimensionContainer.RowDefinitions.Count == 0 ? 0 : 4, 0, 0),
                IsEnabled = Dimensions.IsDimensionAllowed(dimension.Dimension)
            };

            DimensionContainer.RowDefinitions.Add(new RowDefinition()
            {
                Height = new GridLength(0, GridUnitType.Auto)
            });

            Grid.SetRow(button, DimensionContainer.RowDefinitions.Count - 1);
            DimensionContainer.Children.Add(button);
            _dimensions.Add(button);

            return button;
        }

        /// <summary>
        /// The rail button always wears the dimension you are in, so it reads as a state rather
        /// than as a menu. Falls back to the first entry when there is no world, which is what
        /// the scene reports until one is opened.
        /// </summary>
        private void SetCurrentDimensionIcon()
        {
            DimensionUI? current = null;

            foreach (DimensionUI dimension in Dimensions.Dimensions)
            {
                if (dimension.Dimension != Dimensions.CurrentDimension) continue;

                current = dimension;
                break;
            }

            if (current is null && IsCustomDimensionSelected()) current = _extraDimension;

            if (current is null)
            {
                foreach (DimensionUI dimension in Dimensions.Dimensions)
                {
                    current = dimension;
                    break;
                }
            }

            if (current is null) return;

            CurrentDimensionIcon.Source = current.Icon;
            CurrentDimensionButton.ToolTip = $"Dimension: {current.Dimension.Name}";
        }

        private void CurrentDimensionButton_Click(object sender, RoutedEventArgs e)
        {
            AlignPopupToMapEdge();
            DimensionPopup.IsOpen = !DimensionPopup.IsOpen;
        }

        /// <summary>
        /// Pushes the flyout out past the rail so it opens on the map rather than on top of the
        /// rail's own edge, on the same line down the window as the style popup - which sits
        /// <see cref="MAP_EDGE_GAP"/> off the map's left edge.
        /// <para>
        /// Measured from the live tree instead of written into the XAML as a number: the offset
        /// is the distance from this control's right edge to the rail's, and both the rail's
        /// width and the button's inset inside it have been retuned several times already.
        /// </para>
        /// </summary>
        private void AlignPopupToMapEdge()
        {
            RailControl? rail = FindAncestor<RailControl>(this);
            if (rail is null) return;

            Point rootRight = DimensionRoot.TransformToVisual(rail).Transform(new Point(DimensionRoot.ActualWidth, 0));
            DimensionPopup.HorizontalOffset = rail.ActualWidth - rootRight.X + MAP_EDGE_GAP;
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            DependencyObject? current = VisualTreeHelper.GetParent(start);
            while (current is not null and not T) current = VisualTreeHelper.GetParent(current);

            return current as T;
        }

        private void Dimension_MouseDown(object? sender, EventArgs e)
        {
            if (sender is null || sender is not DimensionButtonPanel button) return;

            Dimensions.CurrentDimension = button.Dimension.Dimension;
            DimensionPopup.IsOpen = false;
        }
        private void ExtraDimension_MouseDown(object? sender, EventArgs e)
        {
            CustomDimensionWindow window = new CustomDimensionWindow(Dimensions.ExtraDimensions, Dimensions.CurrentDimension);

            // Beside the flyout, not below it: the rail is on the left edge and the flyout is
            // already the width of one button, so there is room to the right and none beneath.
            Point startupLocation = CurrentDimensionButton.PointToScreen(new(0, 0)).CalibrateToDpiScale();
            startupLocation.X += CurrentDimensionButton.ActualWidth + DimensionContainer.ActualWidth + 12;

            window.Top = startupLocation.Y;
            window.Left = startupLocation.X;

            window.Show();
            window.Closing += (s, ee) =>
            {
                if (window.DialogClosed) return;
                Dimensions.CurrentDimension = window.SelectedDimension;
            };

            DimensionPopup.IsOpen = false;
        }

        private void Dimension_DimensionUpdate(object? sender, EventArgs e)
        {
            SetDimensions();
        }
        private void Dimension_DimensionSelectionChanged(object? sender, EventArgs e)
        {
            foreach (DimensionButtonPanel button in _dimensions)
            {
                if (button.Dimension.Dimension == Dimensions.CurrentDimension)
                {
                    button.Select();
                }
                else
                {
                    button.Deselect();
                }
            }

            if (IsCustomDimensionSelected()) _extraDimensionButton?.Select();

            SetCurrentDimensionIcon();
        }
    }
}
