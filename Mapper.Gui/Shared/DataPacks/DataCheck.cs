using System;
using System.IO;
using System.Linq;
using System.Text;
using Mapper.Gui.Logic;

namespace Mapper.Gui
{
    /// <summary>
    /// <c>Mapee.exe --check-data &lt;Resources folder&gt; [--output &lt;file&gt;]</c>
    /// <para>
    /// Loads a Resources folder with the same code the app uses at startup, without opening a window.
    /// Exit code 0 means this build can use it; 1 means it can't (the reason is printed); 2 is a usage error.
    /// CI runs it before publishing a data pack (scripts/check-data.ps1).
    /// </para>
    /// </summary>
    public static class DataCheck
    {
        public const string Argument = "--check-data";

        public static int Run(string[] args)
        {
            StringBuilder report = new();
            string? outputFile = GetOption(args, "--output");
            int exitCode;

            try
            {
                exitCode = Check(GetOption(args, Argument), report);
            }
            catch (Exception e)
            {
                report.AppendLine($"FAILED: {e.GetType().Name}: {e.Message}");
                report.AppendLine(e.StackTrace);
                exitCode = 1;
            }

            if (outputFile is not null)
            {
                File.WriteAllText(outputFile, report.ToString());
            }
            else
            {
                Console.Out.Write(report.ToString());
                Console.Out.Flush();
            }
            return exitCode;
        }

        private static int Check(string? folder, StringBuilder report)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                report.AppendLine($"Usage: Mapee.exe {Argument} <Resources folder> [--output <file>]");
                return 2;
            }

            string root = Path.GetFullPath(folder);
            report.AppendLine($"Checking {root}");

            DataPackManifest? manifest = DataPackManifest.TryRead(root);
            if (manifest is null)
            {
                report.AppendLine($"FAILED: {DataPackManifest.FileName} is missing or invalid.");
                return 1;
            }
            if (manifest.Format != DataFormat.Supported)
            {
                report.AppendLine($"FAILED: the data is format {manifest.Format}, but this build reads format {DataFormat.Supported}. " +
                                  "Change \"format\" in Resources/manifest.json and DataFormat.Supported together.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(manifest.MinecraftVersion))
            {
                report.AppendLine($"FAILED: {DataPackManifest.FileName} has no \"minecraftVersion\" (the newest Minecraft version the data covers, e.g. \"26.3\").");
                return 1;
            }

            ProgramDomain domain = new(root);

            report.AppendLine($"OK: format {manifest.Format}, revision {manifest.Revision}, Minecraft {manifest.MinecraftVersion}");
            report.AppendLine($"  {domain.Styles.Count} styles: {string.Join(", ", domain.Styles.Select(s => s.Metadata?.Name))}");
            return 0;
        }

        private static string? GetOption(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
