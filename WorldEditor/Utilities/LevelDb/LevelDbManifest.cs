namespace WorldEditor
{
    public class LevelDbManifest
    {
        public readonly struct FileEntry
        {
            public int Level { get; init; }
            public ulong Number { get; init; }
            public byte[] SmallestUserKey { get; init; }
            public byte[] LargestUserKey { get; init; }
        }

        public ulong LogNumber { get; private set; }
        public ulong PrevLogNumber { get; private set; }
        public IList<FileEntry> Files { get; private set; } = [];

        public static LevelDbManifest Read(string directory)
        {
            string current = File.ReadAllText(Path.Combine(directory, "CURRENT")).Trim();
            if (current.Length < 1) throw new InvalidDataException("CURRENT does not name a manifest");

            LevelDbManifest output = new();
            Dictionary<(int, ulong), FileEntry> files = [];

            foreach (byte[] record in LevelDbLogReader.ReadRecords(Path.Combine(directory, current)))
            {
                output.ApplyEdit(record, files);
            }

            output.Files = [.. files.Values];
            return output;
        }

        private void ApplyEdit(byte[] record, Dictionary<(int, ulong), FileEntry> files)
        {
            ReadOnlySpan<byte> data = record;
            int position = 0;

            while (position < data.Length)
            {
                int tag = LevelDbUtilities.ReadVarint32(data, ref position);
                switch (tag)
                {
                    case 1: // comparator name
                        LevelDbUtilities.ReadLengthPrefixed(data, ref position);
                        break;
                    case 2:
                        LogNumber = LevelDbUtilities.ReadVarint64(data, ref position);
                        break;
                    case 3: // next file number
                    case 4: // last sequence
                        LevelDbUtilities.ReadVarint64(data, ref position);
                        break;
                    case 5: // compaction pointer
                        LevelDbUtilities.ReadVarint32(data, ref position);
                        LevelDbUtilities.ReadLengthPrefixed(data, ref position);
                        break;
                    case 6:
                    {
                        int level = LevelDbUtilities.ReadVarint32(data, ref position);
                        ulong number = LevelDbUtilities.ReadVarint64(data, ref position);
                        files.Remove((level, number));
                        break;
                    }
                    case 7:
                    {
                        int level = LevelDbUtilities.ReadVarint32(data, ref position);
                        ulong number = LevelDbUtilities.ReadVarint64(data, ref position);
                        LevelDbUtilities.ReadVarint64(data, ref position); // file size
                        byte[] smallest = LevelDbUtilities.ReadLengthPrefixed(data, ref position);
                        byte[] largest = LevelDbUtilities.ReadLengthPrefixed(data, ref position);

                        files[(level, number)] = new FileEntry()
                        {
                            Level = level,
                            Number = number,
                            SmallestUserKey = LevelDbUtilities.UserKey(smallest).ToArray(),
                            LargestUserKey = LevelDbUtilities.UserKey(largest).ToArray()
                        };
                        break;
                    }
                    case 9:
                        PrevLogNumber = LevelDbUtilities.ReadVarint64(data, ref position);
                        break;
                    default:
                        throw new InvalidDataException($"Unknown manifest tag {tag}");
                }
            }
        }
    }
}
