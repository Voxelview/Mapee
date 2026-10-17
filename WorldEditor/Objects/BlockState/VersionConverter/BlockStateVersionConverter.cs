namespace WorldEditor
{
    public class BlockStateVersionConverter : VersionConverter
    {
        protected override void InitializeConverters()
        {
            Converters.Add(new AlphaBlockStateInstanceConverter());
            Converters.Add(new AnvilBlockStateInstanceConverter()
            {
                From = new VersionRange(Version.Post_1_1, Version.Snapshot_17w46a),
                To = new VersionRange(Version.Snapshot_17w47a, Version.Snapshot_17w47a),
                IdTranslator = IdTranslator.FromFile(Path.Combine("Resources", "WorldEditor", "Translators", "Block", "Flattened.json"))
            });

            Converters.Add(new BlockStateRenamerInstanceConverter()
            {
                From = new VersionRange(Version.Snapshot_17w47a, Version.Newest),
                To = new VersionRange(Version.Snapshot_17w47a, Version.Newest),
                Renamer = BlockRenamer.FromFile(Path.Combine("Resources", "WorldEditor", "Translators", "Block", "Renamed.json"))
            });
        }
    }
}
