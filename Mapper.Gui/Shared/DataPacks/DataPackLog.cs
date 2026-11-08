using System;
using System.IO;

namespace Mapper.Gui
{
    /// <summary>
    /// Appends to %LocalAppData%\Mapee\Logs\data-packs.log. Data updates happen silently, so this is the
    /// place to look when a pack didn't arrive or was rejected. Never throws.
    /// </summary>
    public static class DataPackLog
    {
        private const long MaxSize = 512 * 1024;
        private static readonly object Lock = new();

        public static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mapee", "Logs", "data-packs.log");

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    FileInfo file = new(FilePath);
                    if (file.Exists && file.Length > MaxSize) file.Delete();
                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never break startup.
            }
        }
    }
}
