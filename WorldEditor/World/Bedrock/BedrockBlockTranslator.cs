using System.Collections.Concurrent;
using System.Text.Json;

namespace WorldEditor
{
    // Translates Bedrock block states to the Java states Mapee's assets are written for.
    // Exact states come from Translators/Bedrock/Blocks.json; a state missing from it falls back
    // to the Java name its block maps to, without properties; an unknown block keeps its Bedrock name
    public class BedrockBlockTranslator
    {
        public static string DefaultFile => Path.Combine("Resources", "WorldEditor", "Translators", "Bedrock", "Blocks.json");

        private static readonly Lazy<BedrockBlockTranslator> _default = new(() => FromFile(DefaultFile));
        public static BedrockBlockTranslator Default => _default.Value;

        private readonly Dictionary<string, string> _states;
        private readonly Dictionary<string, string> _names = [];
        private readonly ConcurrentDictionary<string, Block> _cache = new();

        public BedrockBlockTranslator(Dictionary<string, string> states)
        {
            _states = states;
            foreach (var state in states)
            {
                _names.TryAdd(NameOf(state.Key), NameOf(state.Value));
            }
        }

        public static BedrockBlockTranslator FromFile(string file)
        {
            Dictionary<string, string>? states = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            return new BedrockBlockTranslator(states ?? []);
        }

        public Block Translate(Block bedrock)
        {
            string key = StateKey(bedrock);
            if (_cache.TryGetValue(key, out Block cached)) return cached;

            Block output;
            if (_states.TryGetValue(key, out string? java)) output = ParseState(java);
            else if (_names.TryGetValue(bedrock.Name, out string? javaName)) output = new Block(javaName);
            else output = bedrock;

            _cache[key] = output;
            return output;
        }
        public void Translate(Block[] palette)
        {
            for (int i = 0; i < palette.Length; i++)
            {
                palette[i] = Translate(palette[i]);
            }
        }

        // Matches the table's key format: name[a=1,b=2] with properties sorted by name
        private static string StateKey(Block block)
        {
            if (block.Properties.Length == 0) return block.Name + "[]";
            return $"{block.Name}[{string.Join(",", block.Properties.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => $"{p.Name}={p.Value}"))}]";
        }
        private static string NameOf(string state)
        {
            int bracket = state.IndexOf('[');
            return bracket < 0 ? state : state[..bracket];
        }
        private static Block ParseState(string state)
        {
            int bracket = state.IndexOf('[');
            if (bracket < 0) return new Block(state);

            string inner = state[(bracket + 1)..^1];
            if (inner.Length == 0) return new Block(state[..bracket]);

            Property[] properties = inner.Split(',').Select(pair =>
            {
                int equals = pair.IndexOf('=');
                return new Property(pair[..equals], pair[(equals + 1)..]);
            }).ToArray();

            return new Block(state[..bracket], properties);
        }
    }
}
