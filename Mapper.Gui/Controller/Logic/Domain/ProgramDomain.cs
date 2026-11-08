using CommonUtilities.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Mapper.Gui.Logic
{
    public class ProgramDomain
    {
        public AssetPack DefaultAssetPack { get; set; }
        public IList<Style> Styles { get; set; }
        public Style CurrentStyle { get; set; }

        public TexturedAssetPack TexturePack { get; set; }
        public ChunkMapperPack ChunkMapperPack { get; set; }

        public WorldDomain? CurrentWorld { get; set; }

        /// <summary>The Resources folder this domain was loaded from (bundled or a downloaded data pack).</summary>
        public string ResourceRoot { get; }

        /// <param name="resourceRoot">A Resources folder: the one next to Mapee.exe, or an extracted data pack.</param>
        /// <exception cref="Exception">Any file in it is missing or invalid. Loading reads everything up front.</exception>
        public ProgramDomain(string resourceRoot)
        {
            ResourceRoot = resourceRoot;

            AssetPackFactory assetPackFactory = new();
            DefaultAssetPack = assetPackFactory.Create(new DataReader(Path.Combine(resourceRoot, "Mapper", "DefaultAsset")));

            Styles = LoadStyles();
            if (Styles.Count == 0) throw new InvalidDataException($"No valid styles found in {Path.Combine(resourceRoot, "Mapper.Gui", "Styles")}.");
            CurrentStyle = Styles[0];

            TexturePack = LoadTexturePack();
            ChunkMapperPack = new ChunkMapperPack(DefaultAssetPack, Path.Combine(resourceRoot, "WorldEditor", "Translators"));
        }

        private IList<Style> LoadStyles()
        {
            List<Style> styles = new List<Style>();
            string stylesDirectory = Path.Combine(ResourceRoot, "Mapper.Gui", "Styles");

            // A style is a folder or a .zip file. Other files (README, Thumbs.db, ...) are skipped:
            // DataReader would try to open them as zips and fail.
            ISet<string> entries = new HashSet<string>();
            entries.UnionWith(Directory.GetFiles(stylesDirectory, "*.zip"));
            entries.UnionWith(Directory.GetDirectories(stylesDirectory));

            StyleReader styleReader = new(DefaultAssetPack);
            foreach (string entry in entries) 
            {
                IDataReader reader = new DataReader(entry);
                Style? style = styleReader.Read(reader);
                if (style is null) continue;

                styles.Add(style);
            }

            return styles.OrderBy(x => x.Metadata?.OrderedIndex).ToList();
        }

        private TexturedAssetPack LoadTexturePack()
        {
            string colormapDirectory = Path.Combine(ResourceRoot, "Mapper", "Textured", "colormap");
            Colormap colormap = new Colormap(
                new ReadOnlyBitmap(new Uri(Path.Combine(colormapDirectory, "grass.png"), UriKind.Absolute)),
                new ReadOnlyBitmap(new Uri(Path.Combine(colormapDirectory, "foliage.png"), UriKind.Absolute)));

            return new TexturedAssetPack(new Mapper.TexturedAssetPack()
            {
                Colormap = colormap
            });

            //TexturedAssetPackFactory factory = new TexturedAssetPackFactory();
            //TexturedAssetArgs args = new TexturedAssetArgs("Textured", "");

            //Mapper.TexturedAssetPack pack = factory.Create(args);
            //TexturePack = new TexturedAssetPack()
            //{
            //    BlockColorAsset = pack.BlockColorAsset,
            //    BiomeColorAsset = pack.BiomeColorAsset,
            //    FilePath = args.TexturePackPath
            //};
        }
    }
}
