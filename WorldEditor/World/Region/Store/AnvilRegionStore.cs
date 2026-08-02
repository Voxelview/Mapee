namespace WorldEditor
{
    public class AnvilRegionStore : IRegionStore
    {
        public int Count => _store.Count;

        private readonly Dictionary<Coords, string> _store = [];

        public IEnumerable<Coords> Itemize(string directory)
        {
            _store.Clear();

            if (!Directory.Exists(directory)) return [];

            string[] files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                if (!Parser.TryParseRegionName(Path.GetFileName(file), out int regionX, out int regionZ))
                {
                    continue;
                }
                _store[new Coords(regionX, regionZ)] = file;
            }

            return _store.Keys;
        }

        public bool Exists(Coords coords)
        {
            return _store.ContainsKey(coords);
        }

        public bool GetData(Coords coords, out byte[]? buffer, out int length, out StorageFormat format)
        {
            if (_store.TryGetValue(coords, out string? file))
            {
                using (FileStream fileStream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    length = (int)fileStream.Length;
                    buffer = RegionBufferPool.Instance.Rent(length);
                    fileStream.ReadExactly(buffer, 0, length);
                }

                format = Parser.ParseStorageFormat(Path.GetFileName(file));
                return true;
            }
            buffer = null;
            length = 0;
            format = StorageFormat.Anvil;
            return false;
        }

        public byte[]? GetBuffer(Coords coords)
        {
            if (_store.TryGetValue(coords, out string? file))
            {
                return File.ReadAllBytes(file);
            }

            return null;
        }
    }
}
