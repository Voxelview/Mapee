using Mapper.Gui.Model;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for SaveAsImage.xaml
    /// </summary>
    public partial class SaveAsImageControl : Window
    {

        private enum ExportMode { Screenshot, FullResolution }
        private enum ExportBackground { Checker, Black, Transparent }

        // width * height * 4 is the exact Pbgra32 RenderTargetBitmap allocation. WPF's
        // intermediate surface and the encoder roughly triple it at the peak, so 256 MiB of
        // bitmap is about three quarters of a gigabyte of real pressure - the point where a
        // desktop starts to notice. 2 GiB of bitmap is around six, which is the tier that
        // actually fails. Neither blocks the save: this is AnyCPU on 64-bit with no 2 GB wall,
        // and a machine with the memory to spare should be allowed to spend it.
        private const long LARGE_EXPORT_BYTES = 256L * 1024 * 1024;
        private const long HUGE_EXPORT_BYTES = 2L * 1024 * 1024 * 1024;

        // The map's own checker cells are 5 perceived levels apart, which is nearly flat in a
        // 34px patch. 8 keeps the most edges in view and stays whole at 125, 150 and 175
        // percent, so the tiles never land on a half device pixel.
        private const double SWATCH_CHECKER_CELL = 8;

        private readonly IImageSaver _imageSaver;
        private readonly DispatcherTimer _savingAnimationTimer;

        // The whole dialog is these three. Everything on screen is a function of them.
        private ExportMode _mode = ExportMode.Screenshot;
        private bool _clipArea = true;
        private ExportBackground _background = ExportBackground.Checker;

        private bool _initialized;
        private long _outputPixels;
        private int _savingDotCount;
        private bool _isSaving;

        public SaveAsImageControl(IImageSaver imageSaver)
        {
            // Before InitializeComponent, not after. BAML raises Checked as it plays the tree
            // back, and those handlers reach the saver now - assigning afterwards would leave
            // the first one dereferencing null on a field the compiler believes cannot be.
            _imageSaver = imageSaver;

            InitializeComponent();

            _savingAnimationTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _savingAnimationTimer.Tick += SavingAnimationTimer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Must stay first - it resolves template parts, which do not exist until the
            // template has been applied.
            _ = new CustomFrameWindowInitializer(this, Template);

            CheckerSwatch.Background = CreateCheckerBrush(_imageSaver.GetCheckerColors());

            // Writes to the input controls, which raises two events per radio assignment. Every
            // one of them lands on a handler that sets its field and then finds UpdateUi gated.
            SeedFromDefaults();

            _initialized = true;
            UpdateUi();
        }

        /// <summary>
        /// The map's checker, drawn from the pair the exporter will actually use. The three
        /// dimensions each carry their own - the Overworld's is near-black, the Nether's red,
        /// the End's purple - so this cannot be a constant in the XAML.
        /// </summary>
        private static DrawingBrush CreateCheckerBrush(ColorPair colors)
        {
            double cell = SWATCH_CHECKER_CELL;
            double tile = cell * 2;

            GeometryGroup oddCells = new();
            oddCells.Children.Add(new RectangleGeometry(new Rect(cell, 0, cell, cell)));
            oddCells.Children.Add(new RectangleGeometry(new Rect(0, cell, cell, cell)));

            DrawingGroup drawing = new();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Even), null,
                new RectangleGeometry(new Rect(0, 0, tile, tile))));
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colors.Odd), null, oddCells));

            return new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tile, tile),
                ViewportUnits = BrushMappingMode.Absolute
            };
        }

        private void SeedFromDefaults()
        {
            FullResolutionImageArgs defaults = _imageSaver.DefaultFullResArgs;

            // Not expressible in FullResolutionImageArgs - the struct describes a
            // full-resolution export, not which of the two modes is showing.
            _mode = ExportMode.Screenshot;
            _clipArea = defaults.ClipArea;
            _background = ReadBackground(defaults);

            ScreenshotCard.IsChecked = _mode == ExportMode.Screenshot;
            FullResolutionCard.IsChecked = _mode == ExportMode.FullResolution;

            ClipAreaCheckBox.IsChecked = _clipArea;

            CheckerSwatch.IsChecked = _background == ExportBackground.Checker;
            BlackSwatch.IsChecked = _background == ExportBackground.Black;
            TransparentSwatch.IsChecked = _background == ExportBackground.Transparent;
        }

        /// <summary>
        /// The inverse of CreateFullResolutionImageArgs. Routed through one switch on purpose:
        /// three UI states cannot be rebuilt from two independent predicates, and seeding each
        /// swatch from its own would let two of them read true, share a group, and open the
        /// window on whichever was assigned last.
        /// </summary>
        private static ExportBackground ReadBackground(FullResolutionImageArgs args)
        {
            if (args.CheckerPatternEnabled) return ExportBackground.Checker;
            return args.BackgroundColor.A == 0 ? ExportBackground.Transparent : ExportBackground.Black;
        }

        /// <summary>
        /// The single recompute. Every handler sets its field and calls this rather than
        /// patching one label, because the outputs cross-depend: clipping moves the
        /// full-resolution dimensions, which move the warning, which moves the window height.
        ///
        /// It writes to output elements only, never to an input control. That is what makes it
        /// safe to call from every handler without a suppression flag - add an
        /// IsChecked assignment here and the next event will call straight back into it.
        /// </summary>
        private void UpdateUi()
        {
            if (!_initialized) return;

            Size screenshotSize = _imageSaver.GetScreenshotSize();
            Size fullResolutionSize = _imageSaver.GetFullResolutionSize(CreateFullResolutionImageArgs());

            ScreenshotSizeLabel.Text = FormatSize(screenshotSize);
            FullResolutionSizeLabel.Text = FormatSize(fullResolutionSize);

            bool fullResolution = _mode == ExportMode.FullResolution;
            OptionsPanel.Visibility = fullResolution ? Visibility.Visible : Visibility.Collapsed;

            UpdateOutputInfo(fullResolution ? fullResolutionSize : screenshotSize);
            ApplyEnabledState();
        }

        private void UpdateOutputInfo(Size size)
        {
            _outputPixels = (long)size.Width * (long)size.Height;

            if (_outputPixels <= 0)
            {
                OutputInfoLabel.Text = "Nothing to export.";
                WarningRow.Visibility = Visibility.Collapsed;
                return;
            }

            OutputInfoLabel.Text = $"PNG  ·  {size.Width:N0} × {size.Height:N0} px";

            // long, not int: 30000 x 30000 x 4 overflows int32 and wraps negative, which would
            // silently make the largest exports the only ones that never warn.
            long bytes = _outputPixels * 4L;

            if (bytes >= HUGE_EXPORT_BYTES)
            {
                WarningLabel.Text = $"This needs about {FormatBytes(bytes)} of memory while rendering and will " +
                                    "probably fail. Zoom in, or clip to rendered regions.";
                WarningRow.Visibility = Visibility.Visible;
            }
            else if (bytes >= LARGE_EXPORT_BYTES)
            {
                WarningLabel.Text = $"Large export - about {FormatBytes(bytes)} of memory while rendering. " +
                                    "This may take a while.";
                WarningRow.Visibility = Visibility.Visible;
            }
            else
            {
                WarningRow.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// The one owner of IsEnabled. Two things gate it - a save in flight, and an output with
        /// no pixels - and they used to be set from two places that would overwrite each other.
        /// </summary>
        private void ApplyEnabledState()
        {
            // IsEnabled inherits down the tree, so this covers the cards, the options and the
            // buttons, and it will keep covering anything added inside them later. The old
            // seven-name enumeration would have silently missed all of it.
            InteractionRoot.IsEnabled = !_isSaving;
            CancelButton.IsEnabled = !_isSaving;

            // Clipping with nothing loaded, or a canvas that has never laid out, gives a zero
            // dimension - and RenderTargetBitmap throws on one. The card already reads "-", so
            // the dead button is the second half of that sentence rather than a surprise.
            SaveButton.IsEnabled = !_isSaving && _outputPixels > 0;
        }

        /// <summary>
        /// SizeToContent grows the window downward from a fixed Top, so switching to full
        /// resolution walked the dialog off centre. Give back half of what it gained.
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // PreviousSize is 0 on the first pass. That one is CenterScreen's and already right.
            if (!e.HeightChanged || e.PreviousSize.Height == 0) return;

            double top = Top + (e.PreviousSize.Height - e.NewSize.Height) / 2;
            double lowest = SystemParameters.WorkArea.Bottom - e.NewSize.Height;

            Top = Math.Max(SystemParameters.WorkArea.Top, Math.Min(top, lowest));
        }

        private void ScreenshotCard_Checked(object sender, RoutedEventArgs e)
        {
            _mode = ExportMode.Screenshot;
            UpdateUi();
        }
        private void FullResolutionCard_Checked(object sender, RoutedEventArgs e)
        {
            _mode = ExportMode.FullResolution;
            UpdateUi();
        }

        private void ClipAreaCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _clipArea = ClipAreaCheckBox.IsChecked ?? false;
            UpdateUi();
        }

        private void BackgroundSwatch_Checked(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, BlackSwatch)) _background = ExportBackground.Black;
            else if (ReferenceEquals(sender, TransparentSwatch)) _background = ExportBackground.Transparent;
            else _background = ExportBackground.Checker;

            // The background never changes the dimensions - GetFullResolutionSize reads
            // ClipArea and nothing else - but it goes through the same recompute anyway. One
            // path is worth the redundant arithmetic, and it stops being redundant the day
            // the background starts affecting geometry.
            UpdateUi();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// There is no cancellation. A full-resolution render runs to completion on its own STA
        /// thread whatever this window does, so closing here would leave that thread writing a
        /// file for a dialog that no longer exists.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Covers the title bar X as well as Cancel. That button lives in the frame template
            // and calls Window.Close directly, so no amount of IsEnabled on this window's own
            // controls could reach it.
            if (_isSaving) e.Cancel = true;
            base.OnClosing(e);
        }

        private static string FormatSize(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0) return "-";
            return $"{size.Width:N0} × {size.Height:N0} px";
        }

        private static string FormatBytes(long bytes)
        {
            const double MEGABYTE = 1024 * 1024;

            double megabytes = bytes / MEGABYTE;
            return megabytes >= 1024 ? $"{megabytes / 1024:N1} GB" : $"{megabytes:N0} MB";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving) return;

            Microsoft.Win32.SaveFileDialog saveDialog = new()
            {
                FileName = "RenderedMap",
                DefaultExt = ".png",
                Filter = "Image files (.png)|*.png"
            };

            bool? result = saveDialog.ShowDialog();
            if (result == null || !result.Value) return;

            string path = saveDialog.FileName;
            bool saveScreenshot = _mode == ExportMode.Screenshot;
            FullResolutionImageArgs fullResolutionImageArgs = CreateFullResolutionImageArgs();
            Exception? saveException = null;

            BeginSaving();

            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);

                if (saveScreenshot)
                {
                    _imageSaver.SaveAsScreenshot(path);
                }
                else
                {
                    await RunOnStaThreadAsync(() => _imageSaver.SaveAsFullResolution(path, fullResolutionImageArgs));
                }
            }
            catch (Exception exception)
            {
                saveException = exception;
            }
            finally
            {
                EndSaving();
            }

            if (saveException is not null)
            {
                MessageBox.Show(this, $"Image could not be saved.\n\n{saveException.Message}", "Export as image", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Close();
        }

        private void BeginSaving()
        {
            _isSaving = true;
            _savingDotCount = 0;
            // Into the output line rather than a label of its own. Under SizeToContent a
            // Collapsed-to-Visible toggle down here would resize the window mid-save.
            OutputInfoLabel.Text = "Saving";
            _savingAnimationTimer.Start();
            ApplyEnabledState();
        }

        private void EndSaving()
        {
            _savingAnimationTimer.Stop();
            _isSaving = false;
            // Restores the output line and re-applies the zero-size rule in one pass.
            UpdateUi();
        }

        private void SavingAnimationTimer_Tick(object? sender, EventArgs e)
        {
            _savingDotCount = (_savingDotCount + 1) % 4;
            OutputInfoLabel.Text = $"Saving{new string('.', _savingDotCount)}";
        }

        private static Task RunOnStaThreadAsync(Action action)
        {
            TaskCompletionSource<object?> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Thread thread = new(() =>
            {
                try
                {
                    action();
                    taskCompletionSource.SetResult(null);
                }
                catch (Exception exception)
                {
                    taskCompletionSource.SetException(exception);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// The translation between the dialog's three-way background choice and the two fields
        /// the renderer wants. The struct stays as it is: Type and SolidColor are genuinely
        /// orthogonal to the painter, and the squeeze belongs at this boundary.
        /// </summary>
        private FullResolutionImageArgs CreateFullResolutionImageArgs()
        {
            return new FullResolutionImageArgs()
            {
                ClipArea = _clipArea,
                CheckerPatternEnabled = _background == ExportBackground.Checker,
                BackgroundColor = _background switch
                {
                    ExportBackground.Black => Colors.Black,
                    // Checker never reads SolidColor - Draw only reaches it when the pattern is
                    // off - so transparent keeps the struct honest rather than parking an
                    // arbitrary colour in a field nobody looks at.
                    _ => Color.FromArgb(0, 0, 0, 0)
                }
            };
        }
    }
}
