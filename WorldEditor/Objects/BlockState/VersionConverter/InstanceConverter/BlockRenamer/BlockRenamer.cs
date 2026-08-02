namespace WorldEditor
{
    public class BlockRenamer : IBlockRenamer
    {
        private readonly HashSet<string> _fastBlockNameLookup;
        private readonly Dictionary<string, int> _timelineIndices;
        private readonly List<BlockTimeline> _timelines;

        /// <summary>
        /// Same answer as <see cref="_fastBlockNameLookup"/>, memoized per string instance.
        /// Palette names come from per-reader string pools, so the same few hundred
        /// instances recur for every section of every chunk; hashing by reference skips
        /// re-hashing the characters on each of those lookups. New instances fall through
        /// to the real set, so a pool eviction only costs a re-check.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _knownNames = new(ReferenceComparer.Instance);
        private int _knownNamesApprox;

        private sealed class ReferenceComparer : IEqualityComparer<string>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(string? x, string? y) => ReferenceEquals(x, y);
            public int GetHashCode(string obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public BlockRenamer()
        {
            _fastBlockNameLookup = new HashSet<string>();
            _timelineIndices = new Dictionary<string, int>();
            _timelines = new List<BlockTimeline>();
        }

        public bool Rename(Block block, Version to, out Block output)
        {
            output = default;

            if (!_knownNames.TryGetValue(block.Name, out bool mayRename))
            {
                mayRename = _fastBlockNameLookup.Contains(block.Name);

                // String pools overwrite slots on collision, so instances can churn; the cap
                // just stops pathological growth, at the price of uncached lookups past it.
                // Approximate count - ConcurrentDictionary.Count takes every bucket lock.
                if (_knownNamesApprox < 65536 && _knownNames.TryAdd(block.Name, mayRename))
                {
                    Interlocked.Increment(ref _knownNamesApprox);
                }
            }
            if (!mayRename) return false;

            Block sortedBlock = SortProperties(block);
            string blockKey = GetBlockKey(sortedBlock);

            if (!_timelineIndices.TryGetValue(blockKey, out int timelineIndex))
            {
                string simpleKey = block.Name;
                if (!_timelineIndices.TryGetValue(simpleKey, out timelineIndex))
                {
                    return false;
                }
            }

            if (timelineIndex < 0 || timelineIndex >= _timelines.Count) return false;

            BlockTimeline timeline = _timelines[timelineIndex];
            if (!timeline.Find(to, out Block replacement)) return false;

            output = CloneBlock(replacement);
            return true;
        }

        public void AddTimeline(BlockTimeline timeline)
        {
            if (timeline.Blocks.Count == 0) return;

            int timelineIndex = _timelines.Count;
            _timelines.Add(timeline);

            foreach ((Version _, Block block) in timeline.Blocks)
            {
                _fastBlockNameLookup.Add(block.Name);

                Block sortedBlock = SortProperties(block);
                string blockKey = GetBlockKey(sortedBlock);
                _timelineIndices[blockKey] = timelineIndex;
            }
        }

        public static BlockRenamer FromFile(string file)
        {
            return new BlockRenamerReader().Read(File.ReadAllText(file));
        }

        private static string GetBlockKey(Block block)
        {
            if (block.Properties.Length == 0) return block.Name;

            Span<char> buffer = stackalloc char[512];
            int pos = 0;

            block.Name.AsSpan().CopyTo(buffer[pos..]);
            pos += block.Name.Length;
            buffer[pos++] = ' ';

            for (int i = 0; i < block.Properties.Length; i++)
            {
                if (i > 0) buffer[pos++] = ';';

                block.Properties[i].Name.AsSpan().CopyTo(buffer[pos..]);
                pos += block.Properties[i].Name.Length;
                buffer[pos++] = '=';
                block.Properties[i].Value.AsSpan().CopyTo(buffer[pos..]);
                pos += block.Properties[i].Value.Length;
            }

            return new string(buffer[..pos]);
        }

        private static Block SortProperties(Block block)
        {
            if (block.Properties.Length <= 1) return block;

            Property[] sorted = new Property[block.Properties.Length];
            block.Properties.CopyTo(sorted, 0);
            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            return new Block(block.Name, sorted);
        }

        private static Block CloneBlock(Block block)
        {
            Property[] properties = new Property[block.Properties.Length];
            block.Properties.CopyTo(properties, 0);
            return new Block(block.Name, properties);
        }
    }
}
