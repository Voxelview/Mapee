using AutoUpdaterDotNET;
using System;
using System.Reflection;

namespace Mapper.Gui
{
    public static class UpdateChecker
    {
        // Written by the Release workflow (.github/workflows/release.yml) on every release.
        private static readonly string _versionInfoUrl = "https://storage.googleapis.com/mapee_update_bucket_goldentadpole/self_win64/MapeeVersionInfo-self-win64.xml";

        public static void Check()
        {
            // Local builds are 0.0.0 and PR test builds are 0.0.x (see Mapper.Gui.csproj).
            // Only released builds should offer updates.
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null || version.Major == 0) return;

            AutoUpdater.ClearAppDirectory = true;
            AutoUpdater.Start(_versionInfoUrl);
        }
    }
}
