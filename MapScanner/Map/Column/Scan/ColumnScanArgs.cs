namespace MapScanner
{
    public class ColumnScanArgs
    {
        public ISectionCollection SectionCollection { get; set; }
        public ILevelProvider LevelProvider { get; set; }
        public IBlockOutput BlockOutput { get; set; }

        public int X { get; set; }
        public int Z { get; set; }

        public ColumnScanArgs(ISectionCollection sectionCollection, ILevelProvider levelProvider, IBlockOutput blockOutput)
        {
            SectionCollection = sectionCollection;
            LevelProvider = levelProvider;
            BlockOutput = blockOutput;
        }

        public ColumnScanArgs Copy(int x, int z)
        {
            X = x;
            Z = z;

            return this;
        }
    }
}
