using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mapper.Gui
{
    /// <summary>
    /// Downloaded data packs, in %LocalAppData%\Mapee\Data. Per user and writable without admin rights,
    /// and it survives app updates (which wipe the install folder).
    /// <code>
    /// packs\r57\        an extracted pack (a complete Resources folder)
    /// packs\.work-*\    a download in progress
    /// active.json       { "revision": 57 }, the pack to use
    /// rejected.json     { "revisions": [56] }, packs that failed to load; never used or downloaded again
    /// </code>
    /// </summary>
    public sealed class DataPackStore
    {
        private static readonly Regex PackFolderName = new(@"^r\d+$", RegexOptions.Compiled);

        public string Root { get; }
        private string PacksDirectory => Path.Combine(Root, "packs");
        private string ActiveFile => Path.Combine(Root, "active.json");
        private string RejectedFile => Path.Combine(Root, "rejected.json");

        public DataPackStore()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mapee", "Data"))
        {
        }

        public DataPackStore(string root)
        {
            Root = root;
        }

        public string GetPackDirectory(int revision) => Path.Combine(PacksDirectory, $"r{revision}");

        /// <returns>The activated pack, or null if there is none or this build can't use it.</returns>
        public (int Revision, string Directory)? GetActive()
        {
            int revision = ReadJson<ActiveState>(ActiveFile)?.Revision ?? 0;
            if (revision <= 0 || IsRejected(revision)) return null;

            string directory = GetPackDirectory(revision);
            DataPackManifest? manifest = DataPackManifest.TryRead(directory);
            if (manifest is null || manifest.Format != DataFormat.Supported || manifest.Revision != revision) return null;

            return (revision, directory);
        }

        public bool IsRejected(int revision) => ReadJson<RejectedState>(RejectedFile)?.Revisions.Contains(revision) == true;

        /// <summary>Marks a pack as broken, so it's neither used nor downloaded again.</summary>
        public void Reject(int revision)
        {
            RejectedState rejected = ReadJson<RejectedState>(RejectedFile) ?? new RejectedState();
            if (!rejected.Revisions.Contains(revision)) rejected.Revisions.Add(revision);
            WriteJsonAtomically(RejectedFile, rejected);

            if (ReadJson<ActiveState>(ActiveFile)?.Revision == revision) File.Delete(ActiveFile);
        }

        /// <summary>Stops using a pack that's no longer needed (the app's bundled data is newer), so Cleanup deletes it.</summary>
        public void Deactivate(int revision)
        {
            if (ReadJson<ActiveState>(ActiveFile)?.Revision == revision) File.Delete(ActiveFile);
        }

        /// <summary>A fresh folder under packs\ for a download in progress.</summary>
        public string CreateWorkDirectory()
        {
            string directory = Path.Combine(PacksDirectory, $".work-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>Moves a verified, extracted pack into packs\r{revision} and makes it the active pack.</summary>
        public void Install(string extractedDirectory, int revision)
        {
            string target = GetPackDirectory(revision);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(extractedDirectory, target);
            WriteJsonAtomically(ActiveFile, new ActiveState { Revision = revision });
        }

        /// <summary>
        /// Deletes downloaded packs that aren't in use or active, and leftovers of interrupted downloads.
        /// Only touches folders this class creates.
        /// </summary>
        public void Cleanup(int? inUseRevision)
        {
            if (!Directory.Exists(PacksDirectory)) return;

            int? active = ReadJson<ActiveState>(ActiveFile)?.Revision;
            foreach (string directory in Directory.GetDirectories(PacksDirectory))
            {
                string name = Path.GetFileName(directory);
                bool isPack = PackFolderName.IsMatch(name);
                if (!isPack && !name.StartsWith(".work-", StringComparison.Ordinal)) continue;
                if (isPack && (name == $"r{inUseRevision}" || name == $"r{active}")) continue;

                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    DataPackLog.Write($"Couldn't delete old data folder {name}: {e.Message}");
                }
            }
        }

        private static T? ReadJson<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), DataPackManifest.JsonOptions) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        // Write to a temporary file, then rename over the target, so a crash never leaves half a file.
        private static void WriteJsonAtomically<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, DataPackManifest.JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }

        private sealed class ActiveState
        {
            public int Revision { get; set; }
        }

        private sealed class RejectedState
        {
            public List<int> Revisions { get; set; } = new();
        }
    }
}
