namespace WorldEditor
{
    public static class BedrockDimension
    {
        public static int ToId(Dimension dimension)
        {
            if (dimension == Dimension.Nether) return 1;
            if (dimension == Dimension.TheEnd) return 2;
            return 0;
        }
        public static Dimension FromId(int id)
        {
            return id switch
            {
                1 => Dimension.Nether,
                2 => Dimension.TheEnd,
                _ => Dimension.Overworld
            };
        }

        public static short FloorY(int id)
        {
            return (short)(id == 0 ? -64 : 0);
        }
        public static sbyte LowestSection(int id)
        {
            return (sbyte)(FloorY(id) / 16);
        }
    }
}
