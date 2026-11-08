namespace Mapper.Gui
{
    /// <summary>The data pack format this build reads. See RELEASING.md, "Publishing data".</summary>
    public static class DataFormat
    {
        /// <summary>
        /// Bump this together with "format" in Resources/manifest.json whenever the data changes in a way
        /// older builds can't read: new JSON keys or enum values, a new file layout, a changed root type.
        /// A build only downloads data packs of this format, and --check-data refuses any other.
        /// </summary>
        public const int Supported = 1;
    }
}
