namespace WorldEditor
{
    public static class RegionStoreFactory
    {
        public static IRegionStore CreateRegionStore(Level level, Dimension dimension)
        {
            if (level.Edition == Edition.Bedrock)
            {
                return new BedrockRegionStore(BedrockDimension.ToId(dimension));
            }

            return CreateRegionStore(level.Version.Version);
        }
        public static IRegionStore CreateRegionStore(Version version)
        {
            if (version < Version.Post_Beta_1_3)
            {
                return new AlphaRegionStore();
            }

            return new AnvilRegionStore();
        }
    }
}
