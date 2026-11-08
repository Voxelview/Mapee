using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Mapper.Gui
{
    /// <summary>
    /// Shown at startup while a newer data pack downloads. Closes itself when the download finishes or
    /// fails; Skip (or closing the window) cancels it and Mapee starts with the data it already has.
    /// </summary>
    public partial class DataUpdateWindow : Window
    {
        private readonly int _revision;
        private readonly Func<IProgress<double>, CancellationToken, Task> _download;
        private readonly CancellationTokenSource _cancellation = new();
        private bool _finished;

        /// <summary>True if the download completed and the pack was installed.</summary>
        public bool Succeeded { get; private set; }

        /// <summary>Why the download failed, if it did (not set when skipped).</summary>
        public Exception? Error { get; private set; }

        public DataUpdateWindow(int revision, Func<IProgress<double>, CancellationToken, Task> download)
        {
            InitializeComponent();
            _revision = revision;
            _download = download;
            DetailText.Text = $"Data r{revision}";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Progress<double> progress = new(value =>
            {
                DownloadProgress.Value = value;
                DetailText.Text = $"Data r{_revision} · {value:P0}";
            });

            try
            {
                await _download(progress, _cancellation.Token);
                Succeeded = true;
            }
            catch (OperationCanceledException)
            {
                // Skipped, closed, or timed out.
            }
            catch (Exception exception)
            {
                Error = exception;
            }
            finally
            {
                _finished = true;
                if (IsLoaded) Close();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipButton.IsEnabled = false;
            _cancellation.Cancel();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!_finished) _cancellation.Cancel();
        }
    }
}
