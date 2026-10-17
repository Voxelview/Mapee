using System.Text.Json;

namespace WorldEditor
{
    public class BedrockBiomeTranslator
    {
        public static string DefaultFile => Path.Combine("Resources", "WorldEditor", "Translators", "Bedrock", "Biomes.json");
        public static string Fallback => "minecraft:plains";

        private static readonly Lazy<BedrockBiomeTranslator> _default = new(() => FromFile(DefaultFile));
        public static BedrockBiomeTranslator Default => _default.Value;

        private readonly Dictionary<int, string> _biomes;

        public BedrockBiomeTranslator(Dictionary<int, string> biomes)
        {
            _biomes = biomes;
        }

        public static BedrockBiomeTranslator FromFile(string file)
        {
            Dictionary<string, string>? biomes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            return new BedrockBiomeTranslator((biomes ?? []).ToDictionary(x => int.Parse(x.Key), x => x.Value));
        }

        public string Translate(int id)
        {
            return _biomes.TryGetValue(id, out string? name) ? name : Fallback;
        }
    }
}
