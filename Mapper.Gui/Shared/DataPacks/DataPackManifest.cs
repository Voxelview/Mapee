using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mapper.Gui
{
    /// <summary>
    /// manifest.json at the root of a Resources folder, bundled or downloaded: which data format the files
    /// use and which revision they are. The repository's copy only has "format"; packing adds the rest.
    /// </summary>
    public sealed class DataPackManifest
    {
        public const string FileName = "manifest.json";

        public int Format { get; set; }

        /// <summary>Number of commits that changed Resources/. 0 for local builds.</summary>
        public int Revision { get; set; }

        public string? Commit { get; set; }
        public DateTimeOffset? Created { get; set; }

        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        /// <returns>The manifest, or null if the folder has none or it can't be read.</returns>
        public static DataPackManifest? TryRead(string resourceRoot)
        {
            try
            {
                string path = Path.Combine(resourceRoot, FileName);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<DataPackManifest>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>latest.json in the bucket: the newest published pack of one format.</summary>
    public sealed class RemoteDataPack
    {
        public int Format { get; set; }
        public int Revision { get; set; }
        public string Url { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long Size { get; set; }
        public string? Commit { get; set; }
        public DateTimeOffset? Created { get; set; }
    }
}
