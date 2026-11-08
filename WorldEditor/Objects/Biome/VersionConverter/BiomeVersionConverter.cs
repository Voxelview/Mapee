namespace WorldEditor
{
    public class BiomeVersionConverter : VersionConverter
    {
        private readonly string _translatorsDirectory;

        /// <param name="translatorsDirectory">The folder holding the biome Flattened.json and Renamed.json.</param>
        public BiomeVersionConverter(string translatorsDirectory) : base(initialize: false)
        {
            _translatorsDirectory = translatorsDirectory;
            InitializeConverters();
        }

        protected override void InitializeConverters()
        {
            Converters.Add(new Biome2dInstanceConverter()
            {
                From = new VersionRange(Version.Oldest, Version.Snapshot_19w35a),
                To = new VersionRange(Version.Snapshot_19w36a, Version.Snapshot_21w05b),
                Height = 256
            });
            Converters.Add(new BiomeHeightExtenderInstanceConverter()
            {
                From = new VersionRange(Version.Snapshot_19w36a, Version.Snapshot_21w05b),
                To = new VersionRange(Version.Snapshot_21w06a, Version.experimentalSnapshot_1_18_experimentalSnapshot_7),
                OldHeight = 256,
                NewHeight = 384
            });
            Converters.Add(new Biome2dInstanceConverter()
            {
                From = new VersionRange(Version.Oldest, Version.Snapshot_19w35a),
                To = new VersionRange(Version.Snapshot_21w06a, Version.experimentalSnapshot_1_18_experimentalSnapshot_7),
                Height = 384
            });

            Converters.Add(new BiomeChunkInstanceConverter()
            {
                From = new VersionRange(Version.Snapshot_21w06a, Version.experimentalSnapshot_1_18_experimentalSnapshot_7),
                To = new VersionRange(Version.Snapshot_21w37a, Version.Snapshot_21w39a),
                Translator = BiomeIdTranslator.FromFile(Path.Combine(_translatorsDirectory, "Flattened.json"))
            });
            Converters.Add(new BiomeRenamerInstanceConverter()
            {
                From = new VersionRange(Version.Snapshot_21w37a, Version.Newest),
                To = new VersionRange(Version.Snapshot_21w37a, Version.Newest),
                Renamer = BiomeRenamer.FromFile(Path.Combine(_translatorsDirectory, "Renamed.json"))
            });
        }
    }
}
