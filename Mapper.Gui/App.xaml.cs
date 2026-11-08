using System;
using System.IO;
using System.Text;
using System.Windows;
using Mapper.Gui.Logic;

namespace Mapper.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Per-user, writable even when Mapee is installed under Program Files.</summary>
        private static readonly string CrashDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mapee", "Crashes");

        [STAThread]
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0] == DataCheck.Argument)
            {
                Shutdown(DataCheck.Run(e.Args));
                return;
            }

            // The data update window may open and close before the main window exists; with the default
            // mode, closing it would end the app.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptions;
            InitializeBackgroundWork();

            UpdateChecker.Check();

            ProgramDomain domain = DataPacks.LoadAtStartup();
            Controller.Controller controller = new Controller.Controller(domain);
            controller.MainWindow.ShowDialog();

            Shutdown();
        }

        private static void UnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(CrashDirectory);
                string fileName = Path.Combine(CrashDirectory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.txt");

                StringBuilder text = new StringBuilder();
                if (e.ExceptionObject is Exception exception)
                {
                    text.AppendLine(exception.Message);
                    text.Append(exception.StackTrace);
                }

                File.WriteAllText(fileName, text.ToString());
            }
            catch
            {
                // Nothing more can be done while the process is going down.
            }
        }

        private static void InitializeBackgroundWork()
        {
            BackgroundWork.Run(TimeSpan.FromSeconds(5), () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
        }
    }
}
