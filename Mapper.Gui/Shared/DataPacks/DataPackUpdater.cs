using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mapper.Gui
{
    /// <summary>
    /// Finds and downloads data packs published by the "Publish data" workflow
    /// (.github/workflows/publish-data.yml, scripts/build-data-pack.ps1).
    /// </summary>
    public sealed class DataPackUpdater
    {
        // Same bucket as the app's update file (UpdateChecker). Must match scripts/build-data-pack.ps1.
        public const string BucketUrl = "https://storage.googleapis.com/mapee_update_bucket_goldentadpole";

        // A sanity limit: today's pack is a few MB.
        private const long MaxPackSize = 200L * 1024 * 1024;

        private static readonly Regex Sha256Pattern = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly DataPackStore _store;

        public DataPackChannel Channel { get; }

        /// <summary>latest.json for the format this build reads.</summary>
        public Uri LatestUrl => new($"{BucketUrl}/{Channel.BucketFolder()}/format-{DataFormat.Supported}/latest.json");

        public DataPackUpdater(DataPackStore store, DataPackChannel channel)
        {
            _store = store;
            Channel = channel;
        }

        /// <returns>The newest published pack for this build's format, or null if none is published yet.</returns>
        public async Task<RemoteDataPack?> CheckAsync(CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, LatestUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // The bucket answers 403 rather than 404 for a file that doesn't exist (it doesn't allow listing).
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
            response.EnsureSuccessStatusCode();

            RemoteDataPack? pack = await response.Content
                .ReadFromJsonAsync<RemoteDataPack>(DataPackManifest.JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (pack is null) return null;
            if (pack.Format != DataFormat.Supported) throw new InvalidDataException($"latest.json is for format {pack.Format}, expected {DataFormat.Supported}.");
            if (pack.Revision <= 0) throw new InvalidDataException($"latest.json has an invalid revision ({pack.Revision}).");
            if (!pack.Url.StartsWith(BucketUrl + "/", StringComparison.Ordinal)) throw new InvalidDataException($"latest.json points outside the bucket: {pack.Url}");
            if (!Sha256Pattern.IsMatch(pack.Sha256)) throw new InvalidDataException("latest.json has an invalid SHA256.");
            if (pack.Size <= 0 || pack.Size > MaxPackSize) throw new InvalidDataException($"latest.json has an invalid size ({pack.Size}).");

            return pack;
        }

        /// <summary>
        /// Downloads the pack, checks its size, SHA256 and manifest, extracts it and makes it the active pack.
        /// Nothing changes in the store unless every check passes.
        /// </summary>
        /// <param name="progress">Receives 0..1 as the download proceeds.</param>
        public async Task DownloadAndInstallAsync(RemoteDataPack pack, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            string work = _store.CreateWorkDirectory();
            try
            {
                string zipPath = Path.Combine(work, "pack.zip");
                await DownloadAsync(pack, zipPath, progress, cancellationToken).ConfigureAwait(false);

                // Extracting checks every entry stays inside the target folder.
                string extracted = Path.Combine(work, "pack");
                ZipFile.ExtractToDirectory(zipPath, extracted);

                DataPackManifest? manifest = DataPackManifest.TryRead(extracted);
                if (manifest is null) throw new InvalidDataException("The downloaded pack has no manifest.json.");
                if (manifest.Format != pack.Format || manifest.Revision != pack.Revision)
                {
                    throw new InvalidDataException($"The downloaded pack says format {manifest.Format} r{manifest.Revision}, expected format {pack.Format} r{pack.Revision}.");
                }

                _store.Install(extracted, pack.Revision);
                DataPackLog.Write($"Installed data r{pack.Revision} (Minecraft {manifest.MinecraftVersion ?? "?"}, {pack.Size:N0} bytes) from {pack.Url}");
            }
            finally
            {
                try { Directory.Delete(work, recursive: true); } catch { /* Cleanup removes leftovers later. */ }
            }
        }

        private static async Task DownloadAsync(RemoteDataPack pack, string path, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await Http
                .GetAsync(pack.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;

            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (FileStream target = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    received += read;
                    if (received > pack.Size) throw new InvalidDataException($"The download is larger than the expected {pack.Size:N0} bytes.");

                    sha256.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report((double)received / pack.Size);
                }
            }

            if (received != pack.Size) throw new InvalidDataException($"Downloaded {received:N0} bytes, expected {pack.Size:N0}.");

            string actual = Convert.ToHexString(sha256.GetHashAndReset());
            if (!actual.Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The downloaded pack's SHA256 doesn't match latest.json.");
            }
        }

        private static HttpClient CreateHttpClient()
        {
            // Timeouts come from each caller's CancellationToken.
            HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"Mapee/{version?.ToString(3) ?? "0.0.0"}");
            return client;
        }
    }
}
