namespace WorldEditor
{
    // Reads the record framing shared by .log files and the MANIFEST
    public static class LevelDbLogReader
    {
        private const int BLOCK_SIZE = 32768;
        private const int HEADER_SIZE = 7;

        private const byte FULL = 1, FIRST = 2, MIDDLE = 3, LAST = 4;

        public static IEnumerable<byte[]> ReadRecords(string file)
        {
            byte[] data;
            using (FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                data = new byte[stream.Length];
                stream.ReadExactly(data);
            }

            return ReadRecords(data);
        }
        public static IEnumerable<byte[]> ReadRecords(byte[] data)
        {
            List<byte[]> output = [];
            using MemoryStream pending = new();
            bool inFragment = false;

            int position = 0;
            while (position + HEADER_SIZE <= data.Length)
            {
                int leftInBlock = BLOCK_SIZE - position % BLOCK_SIZE;
                if (leftInBlock < HEADER_SIZE)
                {
                    position += leftInBlock;
                    continue;
                }

                int length = data[position + 4] | data[position + 5] << 8;
                byte type = data[position + 6];
                int start = position + HEADER_SIZE;
                if (start + length > data.Length) break;

                // A zero type is preallocated padding; an incomplete tail means the writer was interrupted
                if (type == 0)
                {
                    position += leftInBlock;
                    continue;
                }

                switch (type)
                {
                    case FULL:
                        output.Add(data.AsSpan(start, length).ToArray());
                        inFragment = false;
                        break;
                    case FIRST:
                        pending.SetLength(0);
                        pending.Write(data, start, length);
                        inFragment = true;
                        break;
                    case MIDDLE:
                        if (inFragment) pending.Write(data, start, length);
                        break;
                    case LAST:
                        if (inFragment)
                        {
                            pending.Write(data, start, length);
                            output.Add(pending.ToArray());
                        }
                        inFragment = false;
                        break;
                }

                position = start + length;
            }

            return output;
        }
    }
}
