namespace WorldEditor
{
    /// <summary>
    /// The set of region coordinates a world has stored for one dimension, read from filenames and
    /// nothing else. No region is opened: AnvilRegionStore.Itemize is a directory listing and a
    /// string parse, which is what makes this cheap enough to run across every world in a saves
    /// folder at once.
    ///
    /// Unlike SceneInfo this builds no store to keep, holds no paths and never touches bytes - it
    /// answers "what shape is this world" for a world nobody has opened.
    /// </summary>
    public static class RegionFootprintReader
    {
        /// <summary>
        /// Region coordinates - one unit is 512 blocks. Empty when the world stores nothing in this
        /// dimension, when the region folder cannot be resolved, or when the world predates region
        /// files entirely.
        /// </summary>
        public static IReadOnlyCollection<Coords> Read(Level level, Dimension dimension)
        {
            if (level.Directory is null) return Array.Empty<Coords>();

            // Alpha keeps one file per chunk in base-36 folders, so listing it means a recursive
            // walk of a thousand-odd files - per world, across the whole saves folder. Refused
            // outright; the caller falls back to the world's own icon.
            //
            // A DataVersion below Post_Beta_1_3 says Alpha outright. Version.Unknown does not:
            // LevelVersionReader hands it back for every level.dat with no DataVersion at all,
            // which is Alpha and the McRegion band alike. The Directory.Exists below is what
            // separates those two, because an Alpha world has no region folder to find.
            //
            // Scene.SetWorld tells them apart properly, by sniffing recursively for c.*.*.dat -
            // affordable once, on the world you just picked, and not a hundred times over.
            Version version = level.Version.Version;
            if (version != Version.Unknown && version < Version.Post_Beta_1_3) return Array.Empty<Coords>();

            // Null for a namespace with no colon in it. And note this is not always "region":
            // from Snapshot_26_1_6 on, and for any non-minecraft namespace, even the vanilla
            // overworld lives under dimensions\<namespace>\<name>\region.
            string? folder = dimension.GetRegionFolder(version);
            if (folder is null) return Array.Empty<Coords>();

            string directory = Path.Combine(level.Directory, folder);
            if (!Directory.Exists(directory)) return Array.Empty<Coords>();

            // Not RegionStoreFactory. That maps Version.Unknown onto Anvil, because 0 is not less
            // than Post_Beta_1_3's -200 - which happens to be the right store for the McRegion
            // worlds left in that band, since TryParseRegionName drops four characters without
            // looking at them and r.0.0.mcr therefore parses exactly as r.0.0.mca does. The test
            // above has already turned away the ones it would be wrong for. Deliberate, not
            // inherited.
            AnvilRegionStore store = new();

            // Copied rather than handed back live. Itemize returns the store's own key collection,
            // and the dictionary behind it also holds a full path string per region - copying the
            // keys lets the store and every one of those strings go the moment we return.
            return new List<Coords>(store.Itemize(directory));
        }
    }
}
