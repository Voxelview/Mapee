using Mapper.Gui.Model;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for RenderSettings.xaml
    /// </summary>
    public partial class RenderSettingsControl : Window
    {
        public RenderSettings RenderSettings { get; private set; }
        public bool DialogClosed { get; private set; } = true;

        public event EventHandler? RenderProfileUpdated;

        /// <summary>
        /// A preset is the whole of a render profile, not a lighting pair. Anything a preset does
        /// not have an opinion about - the altitude offset, the transparent step, the background -
        /// comes from the dimension's own defaults, so no preset can carry another dimension's
        /// tuning into this one. That is also what makes Default a complete reset.
        /// </summary>
        private readonly record struct Preset(float SkyLight, float AmbientLight);

        // Only the lighting pair is stated. The Nether ships an altitude offset of 10 and the End
        // 12, so a preset that pinned altitude would be wrong in two dimensions out of three.
        private static readonly Preset FLAT_PRESET = new(0.25f, 0.85f);
        private static readonly Preset CONTRAST_PRESET = new(1.00f, 0.00f);
        private static readonly Preset SOFT_PRESET = new(0.70f, 0.35f);

        private const double VALUE_EPSILON = 0.00005;

        // Neither of these two is bounded in the model, so the track is a comfortable working
        // span rather than a limit. The shipped styles use 0 to 60 for the offset and only 1 or
        // 2 for the step; the rest is headroom, and SetUnboundedSlider widens it further rather
        // than let a slider quietly clamp a value the user never touched.
        private const double ALTITUDE_TRACK_MIN = -64;
        private const double ALTITUDE_TRACK_MAX = 128;
        private const double STEP_TRACK_MIN = 0;
        private const double STEP_TRACK_MAX = 8;

        private readonly RenderSettings _defaultSettings;
        private readonly string _dimensionName;

        private bool _initialized;

        // Slider and text box are two views of one number, and each writes to the other. Without
        // this the first write re-enters the second handler and the caret jumps mid-edit.
        private bool _syncing;

        public RenderSettingsControl(RenderSettings renderSettings, RenderSettings defaultSettings, string dimensionName)
        {
            InitializeComponent();

            RenderSettings = renderSettings;
            _defaultSettings = defaultSettings;
            _dimensionName = dimensionName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CustomFrameWindowInitializer frameInitializer = new(this, Template);
            frameInitializer.SetSecondaryTitle($"For: {_dimensionName}");

            DimensionTagLabel.Text = _dimensionName;
            CheckerBackgroundRadioBox.Background = CreateCheckerBrush(RenderSettings.Background.CheckedColorPair);

            SkyLightDefaultLabel.Text = $"Default {FormatValue(_defaultSettings.SkyLightIntensity)}";
            AmbientLightDefaultLabel.Text = $"Default {FormatValue(_defaultSettings.AmbientLightIntensity)}";
            AltitudeDefaultLabel.Text = $"Default {FormatValue(_defaultSettings.AltitudeYOffset)}";
            StepIntensityDefaultLabel.Text = $"Default {FormatValue(_defaultSettings.SemiTransparentStepIntensity)}";

            Apply(RenderSettings);

            _initialized = true;
            UpdateUi();
        }

        /// <summary>
        /// The map's checker, drawn from the pair this dimension renders with - the Overworld's is
        /// near-black, the Nether's red, the End's purple - so it cannot be a constant in the XAML.
        /// </summary>
        private static DrawingBrush CreateCheckerBrush(ColorPair colors)
        {
            const double CELL = 8;
            const double TILE = CELL * 2;

            GeometryGroup oddCells = new();
            oddCells.Children.Add(new RectangleGeometry(new Rect(CELL, 0, CELL, CELL)));
            oddCells.Children.Add(new RectangleGeometry(new Rect(0, CELL, CELL, CELL)));

            DrawingGroup drawing = new();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Even), null,
                new RectangleGeometry(new Rect(0, 0, TILE, TILE))));
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Odd), null, oddCells));

            return new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, TILE, TILE),
                ViewportUnits = BrushMappingMode.Absolute
            };
        }

        /// <summary>
        /// Writes a whole profile into the controls. The only method that touches inputs after
        /// load - everything else reads them - so it is also the only one that has to suppress
        /// the change handlers it sets off.
        /// </summary>
        private void Apply(RenderSettings settings)
        {
            _syncing = true;

            SkyLightSlider.Value = settings.SkyLightIntensity;
            AmbientLightSlider.Value = settings.AmbientLightIntensity;
            SetUnboundedSlider(AltitudeSlider, settings.AltitudeYOffset, ALTITUDE_TRACK_MIN, ALTITUDE_TRACK_MAX);
            SetUnboundedSlider(StepIntensitySlider, settings.SemiTransparentStepIntensity, STEP_TRACK_MIN, STEP_TRACK_MAX);

            SkyLightTextBox.Text = FormatValue(settings.SkyLightIntensity);
            AmbientLightTextBox.Text = FormatValue(settings.AmbientLightIntensity);
            AltitudeTextBox.Text = FormatValue(settings.AltitudeYOffset);
            StepIntensityTextBox.Text = FormatValue(settings.SemiTransparentStepIntensity);

            CheckerBackgroundRadioBox.IsChecked = settings.Background.Type == BackgroundType.Checker;
            SolidBackgroundRadioBox.IsChecked = settings.Background.Type == BackgroundType.Solid;

            _syncing = false;
        }

        private RenderSettings ReadSettings()
        {
            BackgroundType type = SolidBackgroundRadioBox.IsChecked == true ? BackgroundType.Solid : BackgroundType.Checker;

            return new RenderSettings()
            {
                SkyLightIntensity = ParseValue(SkyLightTextBox.Text, RenderSettings.SkyLightIntensity),
                AmbientLightIntensity = ParseValue(AmbientLightTextBox.Text, RenderSettings.AmbientLightIntensity),
                AltitudeYOffset = ParseValue(AltitudeTextBox.Text, RenderSettings.AltitudeYOffset),
                SemiTransparentStepIntensity = ParseValue(StepIntensityTextBox.Text, RenderSettings.SemiTransparentStepIntensity),
                Background = new Background()
                {
                    Type = type,
                    CheckedColorPair = RenderSettings.Background.CheckedColorPair,
                    SolidColor = type == BackgroundType.Solid ? Colors.Black : RenderSettings.Background.SolidColor
                }
            };
        }

        // ---- handlers -------------------------------------------------------------------

        private void Preset_Checked(object sender, RoutedEventArgs e)
        {
            if (!_initialized || _syncing) return;

            RenderSettings settings = _defaultSettings;

            if (ReferenceEquals(sender, FlatPreset)) settings = WithLighting(settings, FLAT_PRESET);
            else if (ReferenceEquals(sender, ContrastPreset)) settings = WithLighting(settings, CONTRAST_PRESET);
            else if (ReferenceEquals(sender, SoftPreset)) settings = WithLighting(settings, SOFT_PRESET);

            Apply(settings);
            UpdateUi();
        }

        private static RenderSettings WithLighting(RenderSettings settings, Preset preset)
        {
            settings.SkyLightIntensity = preset.SkyLight;
            settings.AmbientLightIntensity = preset.AmbientLight;
            return settings;
        }

        /// <summary>
        /// A track whose ends are a convenience, not a rule. If a value arrives from outside the
        /// working span the span grows to hold it, because a slider that silently pulls a number
        /// back inside its own bounds is a slider that edits a setting nobody asked it to.
        /// </summary>
        private static void SetUnboundedSlider(Slider slider, float value, double min, double max)
        {
            slider.Minimum = Math.Min(min, value);
            slider.Maximum = Math.Max(max, value);
            slider.Value = value;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized || _syncing) return;

            _syncing = true;

            if (ReferenceEquals(sender, SkyLightSlider)) SkyLightTextBox.Text = FormatValue((float)e.NewValue);
            else if (ReferenceEquals(sender, AmbientLightSlider)) AmbientLightTextBox.Text = FormatValue((float)e.NewValue);
            else if (ReferenceEquals(sender, AltitudeSlider)) AltitudeTextBox.Text = FormatValue((float)e.NewValue);
            else if (ReferenceEquals(sender, StepIntensitySlider)) StepIntensityTextBox.Text = FormatValue((float)e.NewValue);

            _syncing = false;

            UpdateUi();
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_initialized || _syncing) return;

            _syncing = true;

            // Only a value the box agrees is valid reaches its slider. While the text is
            // half-typed or out of range the thumb simply holds still.
            if (ReferenceEquals(sender, SkyLightTextBox) && InputValidator.ValidateSingle(SkyLightTextBox.Text, 0, 1))
            {
                SkyLightSlider.Value = ParseValue(SkyLightTextBox.Text, 0);
            }
            else if (ReferenceEquals(sender, AmbientLightTextBox) && InputValidator.ValidateSingle(AmbientLightTextBox.Text, 0, 1))
            {
                AmbientLightSlider.Value = ParseValue(AmbientLightTextBox.Text, 0);
            }
            else if (ReferenceEquals(sender, AltitudeTextBox) && InputValidator.ValidateSingle(AltitudeTextBox.Text))
            {
                SetUnboundedSlider(AltitudeSlider, ParseValue(AltitudeTextBox.Text, 0), ALTITUDE_TRACK_MIN, ALTITUDE_TRACK_MAX);
            }
            else if (ReferenceEquals(sender, StepIntensityTextBox) && InputValidator.ValidateSingle(StepIntensityTextBox.Text))
            {
                SetUnboundedSlider(StepIntensitySlider, ParseValue(StepIntensityTextBox.Text, 0), STEP_TRACK_MIN, STEP_TRACK_MAX);
            }

            _syncing = false;

            UpdateUi();
        }

        private void BackgroundSwatch_Checked(object sender, RoutedEventArgs e)
        {
            if (!_initialized || _syncing) return;
            UpdateUi();
        }

        private void FineTuningToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (FineTuningPanel is null) return;

            // Collapsed rather than Hidden - a collapsed element measures 0x0 and its margin goes
            // with it, so the window closes back to exactly the preset grid.
            FineTuningPanel.Visibility = FineTuningToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdateUi();
        }

        /// <summary>
        /// SizeToContent grows the window downward from a fixed Top, so opening fine tuning walks
        /// the dialog off centre. Give back half of what it gained.
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // PreviousSize is 0 on the first pass. That one is CenterScreen's and already right.
            if (!e.HeightChanged || e.PreviousSize.Height == 0) return;

            double top = Top + (e.PreviousSize.Height - e.NewSize.Height) / 2;
            double lowest = SystemParameters.WorkArea.Bottom - e.NewSize.Height;

            Top = Math.Max(SystemParameters.WorkArea.Top, Math.Min(top, lowest));
        }

        // ---- recompute ------------------------------------------------------------------

        /// <summary>
        /// The single recompute. Writes to outputs only - never back into an input - which is what
        /// lets every handler call it without a second suppression flag.
        /// </summary>
        private void UpdateUi()
        {
            if (!_initialized) return;

            string? error = Validate();
            bool valid = error is null;

            ValidationLabel.Text = error ?? string.Empty;
            ValidationLabel.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
            RenderButton.IsEnabled = valid;

            MarkField(SkyLightTextBox, InputValidator.ValidateSingle(SkyLightTextBox.Text, 0, 1));
            MarkField(AmbientLightTextBox, InputValidator.ValidateSingle(AmbientLightTextBox.Text, 0, 1));
            MarkField(AltitudeTextBox, InputValidator.ValidateSingle(AltitudeTextBox.Text));
            MarkField(StepIntensityTextBox, InputValidator.ValidateSingle(StepIntensityTextBox.Text));

            UpdatePresetSelection(valid);
            UpdateFineTuningHint();
        }

        private void UpdatePresetSelection(bool valid)
        {
            RenderSettings current = valid ? ReadSettings() : default;

            bool isDefault = valid && Matches(current, _defaultSettings);
            bool isFlat = valid && Matches(current, WithLighting(_defaultSettings, FLAT_PRESET));
            bool isContrast = valid && Matches(current, WithLighting(_defaultSettings, CONTRAST_PRESET));
            bool isSoft = valid && Matches(current, WithLighting(_defaultSettings, SOFT_PRESET));

            // The cards are inputs, so this is the one place UpdateUi writes to one. Suppressed,
            // because unchecking a radio in a group raises Checked on nothing but Unchecked on it.
            _syncing = true;
            DefaultPreset.IsChecked = isDefault;
            FlatPreset.IsChecked = isFlat;
            ContrastPreset.IsChecked = isContrast;
            SoftPreset.IsChecked = isSoft;
            _syncing = false;

            CustomLabel.Visibility = isDefault || isFlat || isContrast || isSoft
                ? Visibility.Hidden
                : Visibility.Visible;
        }

        private void UpdateFineTuningHint()
        {
            if (FineTuningToggle.IsChecked == true)
            {
                FineTuningToggle.Content = "Hide";
                return;
            }

            // Named the same as the rows behind it, so the summary and the panel agree.
            FineTuningToggle.Content = $"Sky {SkyLightTextBox.Text}  ·  Ambient {AmbientLightTextBox.Text}  ·  " +
                                       $"Altitude {AltitudeTextBox.Text}  ·  Step {StepIntensityTextBox.Text}";
        }

        private string? Validate()
        {
            if (!InputValidator.ValidateSingle(SkyLightTextBox.Text, 0, 1)) return "Sky light must be a number between 0 and 1.";
            if (!InputValidator.ValidateSingle(AmbientLightTextBox.Text, 0, 1)) return "Ambient light must be a number between 0 and 1.";
            if (!InputValidator.ValidateSingle(AltitudeTextBox.Text)) return "Altitude Y offset must be a number.";
            if (!InputValidator.ValidateSingle(StepIntensityTextBox.Text)) return "Transparent step must be a number.";

            return null;
        }

        // TextBoxStyle drives its border from template triggers, and a trigger on a named template
        // part beats a local value on the control - so the border cannot carry this. The text can:
        // Foreground is a plain style setter there, and a local value wins over one of those.
        private static readonly SolidColorBrush FIELD_BRUSH = new(Color.FromRgb(238, 238, 238));
        private static readonly SolidColorBrush FIELD_INVALID_BRUSH = new(Color.FromRgb(229, 138, 130));

        private static void MarkField(TextBox field, bool valid)
        {
            field.Foreground = valid ? FIELD_BRUSH : FIELD_INVALID_BRUSH;
        }

        private static bool Matches(RenderSettings left, RenderSettings right)
        {
            return Near(left.SkyLightIntensity, right.SkyLightIntensity)
                && Near(left.AmbientLightIntensity, right.AmbientLightIntensity)
                && Near(left.AltitudeYOffset, right.AltitudeYOffset)
                && Near(left.SemiTransparentStepIntensity, right.SemiTransparentStepIntensity)
                && left.Background.Type == right.Background.Type;
        }

        private static bool Near(float left, float right)
        {
            return Math.Abs(left - right) < VALUE_EPSILON;
        }

        // ---- render ---------------------------------------------------------------------

        private void Render_Click(object sender, RoutedEventArgs e)
        {
            // Belt and braces. The button is disabled while anything is invalid, so this only
            // fires if Enter reached us through a path that skipped the recompute.
            if (Validate() is not null) return;

            RenderSettings = ReadSettings();

            DialogClosed = false;
            RenderProfileUpdated?.Invoke(this, EventArgs.Empty);

            if (CloseWindowCheckBox.IsChecked ?? true) Close();
            else UpdateUi();
        }

        // ---- formatting -----------------------------------------------------------------

        private static string FormatValue(float value)
        {
            // Four places, not two. At two the Overworld's shipped ambient of 0.125 displayed as
            // "0.13", which is the value that would then be committed - so opening this window
            // and pressing Render without touching anything quietly moved the setting, and the
            // preset match failed against a default the box could no longer express.
            // Trailing zeros are still trimmed, so an altitude of 10 is "10" and not "10.0000".
            return Math.Round(value, 4).ToString("0.####", CultureInfo.CurrentCulture);
        }

        private static float ParseValue(string text, float fallback)
        {
            if (!float.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
            {
                return fallback;
            }

            return value;
        }
    }
}
