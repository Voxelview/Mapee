using NbtEditor;

namespace WorldEditor {
    public class Level {
        public string? Directory { get; set; }
        public Edition Edition { get; set; }
        public CompoundTag? Tag { get; set; }

        public LevelVersion Version { get; set; }
        public Player Player { get; set; }
        public WorldGen WorldGen { get; set; }

        public IList<string> DataPacks { get; set; }

        public string? WorldName { get; set; }
        public GameType GameType { get; set; }
        public bool AllowCommands { get; set; }
        public bool IsHardcode { get; set; }
        public DateTime LastPlayed { get; set; }

        public Level() {
            DataPacks = new List<string>();
        }
    }
}
