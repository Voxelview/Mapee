using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mapper.Gui.Logic;

namespace Mapper.Gui
{
    /// <summary>
    /// Picks and loads the block, biome and style data at startup: the Resources folder bundled with the
    /// app, or a newer data pack from the bucket. See RELEASING.md, "Publishing data".
    /// </summary>
    public static class DataPacks
    {
        // Longest startup waits for the bucket to answer; after that, startup goes ahead and a newer pack
        // is downloaded in the background for next time.
        private static readonly TimeSpan CheckWait = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

        /// <summary>The Resources folder next to Mapee.exe.</summary>
        public static string BundledRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Resources");

        /// <summary>Revision of the data in use, shown in the title bar. 0 for local builds.</summary>
        public static int LoadedRevision { get; private set; }

        /// <summary>
        /// Checks for a newer pack (downloading it with a progress window if there is one), then loads the
        /// newest usable data. A downloaded pack that fails to load is rejected and the bundled data is used.
        /// </summary>
        /// <exception cref="Exception">The bundled data itself failed to load.</exception>
        public static ProgramDomain LoadAtStartup()
        {
            DataPackStore store = new();
            int bundledRevision = DataPackManifest.TryRead(BundledRoot)?.Revision ?? 0;

            DataPackChannel? channel = DataPackChannels.FromEnvironment();
            DataPackUpdater? updater = channel is null ? null : new DataPackUpdater(store, channel.Value);

            Task<RemoteDataPack?>? check = null;
            if (updater is not null)
            {
                check = Task.Run(() => CheckQuietlyAsync(updater));
                if (check.Wait(CheckWait) && check.Result is { } remote && IsNewer(remote, store, bundledRevision))
                {
                    DownloadWithWindow(updater, remote);
                }
            }

            ProgramDomain domain = LoadNewest(store, bundledRevision);
            store.Cleanup(domain.ResourceRoot == BundledRoot ? null : LoadedRevision);

            // The bucket didn't answer in time: finish in the background and use the pack next time.
            if (check is not null && !check.IsCompleted)
            {
                _ = DownloadInBackgroundAsync(updater!, store, bundledRevision, check);
            }

            return domain;
        }

        private static bool IsNewer(RemoteDataPack remote, DataPackStore store, int bundledRevision)
        {
            int current = Math.Max(bundledRevision, store.GetActive()?.Revision ?? 0);
            return remote.Revision > current && !store.IsRejected(remote.Revision);
        }

        private static async Task<RemoteDataPack?> CheckQuietlyAsync(DataPackUpdater updater)
        {
            try
            {
                using CancellationTokenSource timeout = new(CheckTimeout);
                return await updater.CheckAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                DataPackLog.Write($"Couldn't check {updater.LatestUrl}: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        private static void DownloadWithWindow(DataPackUpdater updater, RemoteDataPack remote)
        {
            DataUpdateWindow window = new(remote.Revision, async (progress, cancellation) =>
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(DownloadTimeout);
                // Off the UI thread, so the window stays responsive.
                await Task.Run(() => updater.DownloadAndInstallAsync(remote, progress, timeout.Token), timeout.Token);
            });
            window.ShowDialog();

            if (window.Error is not null) DataPackLog.Write($"Couldn't download data r{remote.Revision}: {window.Error.GetType().Name}: {window.Error.Message}");
            else if (!window.Succeeded) DataPackLog.Write($"Skipped downloading data r{remote.Revision}.");
        }

        private static async Task DownloadInBackgroundAsync(DataPackUpdater updater, DataPackStore store, int bundledRevision, Task<RemoteDataPack?> check)
        {
            try
            {
                RemoteDataPack? remote = await check.ConfigureAwait(false);
                if (remote is null || !IsNewer(remote, store, bundledRevision)) return;

                using CancellationTokenSource timeout = new(DownloadTimeout);
                await updater.DownloadAndInstallAsync(remote, null, timeout.Token).ConfigureAwait(false);
                DataPackLog.Write($"Data r{remote.Revision} will be used from the next start.");
            }
            catch (Exception e)
            {
                DataPackLog.Write($"Background data download failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private static ProgramDomain LoadNewest(DataPackStore store, int bundledRevision)
        {
            (int Revision, string Directory)? active = store.GetActive();
            if (active is { } obsolete && obsolete.Revision <= bundledRevision)
            {
                // An app update brought newer data than the downloaded pack.
                store.Deactivate(obsolete.Revision);
                DataPackLog.Write($"The app's bundled data (r{bundledRevision}) replaces downloaded r{obsolete.Revision}.");
            }
            else if (active is { } pack)
            {
                try
                {
                    ProgramDomain domain = new(pack.Directory);
                    LoadedRevision = pack.Revision;
                    return domain;
                }
                catch (Exception e)
                {
                    // A pack that passed CI can still be broken for this build; never let it stop Mapee starting.
                    store.Reject(pack.Revision);
                    DataPackLog.Write($"Data r{pack.Revision} failed to load and was rejected; using the bundled data. {e.GetType().Name}: {e.Message}");
                }
            }

            LoadedRevision = bundledRevision;
            return new ProgramDomain(BundledRoot);
        }
    }
}
