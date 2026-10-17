using NbtEditor;
using System.Numerics;
using System.Text;

namespace WorldEditor
{
    // Bedrock's level.dat is an 8 byte header (storage version, length) followed by uncompressed little-endian NBT
    public class BedrockLevelReader : IObjectReader<string, Level?>
    {
        public bool ReadPlayer { get; set; } = true;

        public static bool IsBedrockWorld(string directory)
        {
            return File.Exists(Path.Combine(directory, "level.dat")) && Directory.Exists(Path.Combine(directory, "db"));
        }

        public Level? Read(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length < 9) return null;

            if (ReadTag(bytes, 8) is not CompoundTag data) return null;

            string directory = Path.GetDirectoryName(file) ?? string.Empty;
            Level output = new()
            {
                Tag = data,
                Directory = directory,
                Edition = Edition.Bedrock,
                Version = new LevelVersion()
                {
                    Version = Version.Newest,
                    VersionName = ReadVersionName(data)
                },
                WorldName = ReadWorldName(data, directory),
                GameType = data["GameType"] is Int32Tag gameType ? (GameType)(int)gameType : GameType.Survival,
                AllowCommands = data["commandsEnabled"] is SignedByteTag commands && (sbyte)commands == 1,
                IsHardcode = data["IsHardcore"] is SignedByteTag hardcore && (sbyte)hardcore == 1,
                LastPlayed = data["LastPlayed"] is Int64Tag lastPlayed ? DateTimeOffset.FromUnixTimeSeconds(lastPlayed).UtcDateTime : DateTime.MinValue
            };

            Vector3 worldSpawn = new(ReadInt(data, "SpawnX"), ReadInt(data, "SpawnY"), ReadInt(data, "SpawnZ"));
            output.WorldGen = new WorldGen()
            {
                WorldSpawn = worldSpawn,
                Seed = data["RandomSeed"] is Int64Tag seed ? seed : 0
            };
            output.Player = new Player()
            {
                Position = worldSpawn,
                Spawn = worldSpawn,
                Dimension = Dimension.Overworld,
                GameType = output.GameType
            };

            if (ReadPlayer)
            {
                try
                {
                    ReadLocalPlayer(directory, output);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Couldn't read the local player of {directory}: {e.Message}");
                }
            }

            return output;
        }

        private static void ReadLocalPlayer(string directory, Level level)
        {
            using LevelDbReader db = new(Path.Combine(directory, "db"));
            if (!db.TryGet(Encoding.ASCII.GetBytes("~local_player"), out byte[]? value) || value is null) return;
            if (ReadTag(value, 0) is not CompoundTag player) return;

            Player output = level.Player;
            if (player["Pos"] is ListTag pos && pos.Count == 3)
            {
                output.Position = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
            }
            if (player["DimensionId"] is Int32Tag dimension)
            {
                output.Dimension = BedrockDimension.FromId(dimension);
            }

            // An unset player spawn is stored as int.MinValue
            int spawnX = ReadInt(player, "SpawnX"), spawnZ = ReadInt(player, "SpawnZ");
            if (spawnX != int.MinValue && spawnZ != int.MinValue)
            {
                output.Spawn = new Vector3(spawnX, ReadInt(player, "SpawnY"), spawnZ);
            }

            level.Player = output;
        }

        private static Tag ReadTag(byte[] bytes, int index)
        {
            UnsafeNbtReader reader = new(bytes, index) { IsFormatLittleEndian = true };
            return Tag.FromNbtReader(reader);
        }
        private static int ReadInt(CompoundTag tag, string name)
        {
            return tag[name] is Int32Tag value ? value : 0;
        }
        private static string ReadWorldName(CompoundTag data, string directory)
        {
            string levelNameFile = Path.Combine(directory, "levelname.txt");
            if (File.Exists(levelNameFile))
            {
                string name = File.ReadAllText(levelNameFile).Trim();
                if (name.Length > 0) return name;
            }

            return data["LevelName"] is StringTag levelName ? levelName : Path.GetFileName(directory);
        }
        private static string ReadVersionName(CompoundTag data)
        {
            if (data["lastOpenedWithVersion"] is not ListTag version || version.Count < 3) return "Bedrock";

            IEnumerable<int> parts = version.Take(version.Count).Select(x => (int)x);
            return $"Bedrock {string.Join(".", parts.Take(3))}";
        }
    }
}
