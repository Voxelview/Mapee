using System;
using System.Reflection;

namespace Mapper.Gui
{
    /// <summary>Which published data packs this app downloads.</summary>
    public enum DataPackChannel
    {
        /// <summary>Packs published to all users (bucket folder data/).</summary>
        Live,

        /// <summary>Packs published for testing (bucket folder data-test/).</summary>
        Test
    }

    public static class DataPackChannels
    {
        public const string EnvironmentVariable = "MAPEE_DATA_CHANNEL";

        /// <summary>
        /// MAPEE_DATA_CHANNEL=live|test|off overrides the default. By default released builds use the
        /// live channel and local or PR test builds (version 0.x) don't download data at all.
        /// </summary>
        /// <returns>The channel, or null if data packs are off.</returns>
        public static DataPackChannel? FromEnvironment()
        {
            switch (Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim().ToLowerInvariant())
            {
                case "live": return DataPackChannel.Live;
                case "test": return DataPackChannel.Test;
                case "off": return null;
            }

            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version is not null && version.Major > 0 ? DataPackChannel.Live : null;
        }

        /// <summary>The bucket folder holding this channel's packs.</summary>
        public static string BucketFolder(this DataPackChannel channel) => channel switch
        {
            DataPackChannel.Test => "data-test",
            _ => "data"
        };
    }
}
