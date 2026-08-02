using Mapper.Gui.Model;
using MapScanner;
using System.Collections.Generic;
using WorldEditor;

namespace Mapper.Gui.Logic
{
    public class ChunkMapperPack : MapperPack
    {
        public virtual AssetPack AssetPack { get; set; }
        public virtual IList<string> NbtHeightmaps { get; set; }

        private ChunkScanner? _chunkScanner;
        private SharedColumnScanArgsFactory? _scannerFactory;
        private ChunkBlockControllerFactory? _renderFactory;

        private StepChunkScanner? _stepChunkScanner;

        private SemiTransparentStepPainter? _semiTransparentStepPainter;
        private LightPainter? _lightPainter;
        private ElevationPainter? _elevationPainter;

        public ChunkMapperPack(AssetPack assetPack)
        {
            AssetPack = assetPack;
            NbtHeightmaps = new List<string>() 
            {
                "WORLD_SURFACE",
                "OCEAN_FLOOR",
                "MOTION_BLOCKING",
                "MOTION_BLOCKING_NO_LEAVES"
            };

            InitializePack();
        }

        protected void InitializePack()
        {
            ChunkReader = CreateChunkReader();
            VersionConverter = CreateVersionConverter();
            ChunkScanner = CreateChunkScanner();
            StepChunkScanner = CreateStepChunkScanner();
            MapRenderer = CreateMapRenderer();
        }

        protected IObjectReader<ChunkParamater, IChunk?> CreateChunkReader()
        {
            ApiChunkLoadOptions options = new()
            {
                KeepDataTag = false
            };

            options.BiomeOptions.LoadObject = true;
            options.BiomeOptions.KeepDataTag = false;

            options.BlockStateOptions.LoadObject = true;
            options.BlockStateOptions.KeepDataTag = false;

            options.BlockLightOptions.LoadObject = true;
            options.BlockLightOptions.KeepDataTag = false;

            options.SkyLightOptions.LoadObject = true;
            options.SkyLightOptions.KeepDataTag = false;

            options.HeightmapOptions.LoadObject = true;
            options.HeightmapOptions.KeepDataTag = false;

            return new ApiChunkReader()
            {
                LoadOptions = options
            };
        }
        protected IVersionConverter CreateVersionConverter()
        {
            return new ApiChunkVersionConverter();
        }
        protected IObjectScanner<ConvertedApiChunk, IScannedChunk> CreateChunkScanner()
        {
            _scannerFactory = new SharedColumnScanArgsFactory(AssetPack.BlockGroupingAsset, WorldMapper.SCAN_SLOTS);

            _chunkScanner = new ChunkScanner(new ColumnScanner(), _scannerFactory);
            return _chunkScanner;
        }
        protected IObjectScanner<IScannedChunk, StepChunk> CreateStepChunkScanner()
        {
            _stepChunkScanner = new StepChunkScanner(AssetPack.StepTypeAsset);
            return _stepChunkScanner;
        }
        protected IMapRenderer<MapRenderArgs> CreateMapRenderer()
        {
            _renderFactory = new ChunkBlockControllerFactory(AssetPack);
            _lightPainter = new LightPainter();
            _elevationPainter = new ElevationPainter();

            BlockPainter solidBlockPainter = new BlockPainter() 
            {
                LightPainter = _lightPainter,
                ElevationPainter = _elevationPainter
            };
            StopAtEncounterColumnRenderer solidColumnRenderer = new StopAtEncounterColumnRenderer()
            {
                BlockPainter = solidBlockPainter
            };

            _semiTransparentStepPainter = new SemiTransparentStepPainter();
            SemiTransparentBlockPainter semiTransparentBlockPainter = new SemiTransparentBlockPainter() 
            {
                LightPainter = _lightPainter,
                ElevationPainter = _elevationPainter,
                StepPainter = _semiTransparentStepPainter
            };
            SemiTransparentColumnRenderer transparentColumnRenderer = new SemiTransparentColumnRenderer() 
            {
                BlockPainter = semiTransparentBlockPainter
            };

            ColumnRenderer columRenderer = new ColumnRenderer() 
            {
                StopAtEncounterColumnRenderer = solidColumnRenderer,
                SemiTransparentColumnRenderer = transparentColumnRenderer
            };

            return new MapRenderer(new ChunkRenderer(columRenderer, _renderFactory));
        }

        public virtual void UpdatePack(Profile profile, ScanType scanType) 
        {
            if (_chunkScanner is null || _scannerFactory is null || _renderFactory is null ||
                _stepChunkScanner is null || _lightPainter is null || _elevationPainter is null || _semiTransparentStepPainter is null) return;

            _scannerFactory.ScanType = scanType;
            _scannerFactory.Asset = profile.AssetPack.BlockGroupingAsset;
            _scannerFactory.UseHeightmap = profile.HeightmapSettings.HeightmapType == HeightmapType.NbtHeightmap;
            _scannerFactory.Heightmap = profile.HeightmapSettings.NbtHeightmap;
            _scannerFactory.SetY = profile.HeightmapSettings.SetY;

            _stepChunkScanner.Asset = profile.AssetPack.StepTypeAsset;

            _renderFactory.AssetPack = profile.AssetPack;
            _lightPainter.SunIntensity = profile.RenderSettings.SkyLightIntensity;
            _lightPainter.AmbientLight = profile.RenderSettings.AmbientLightIntensity;
            _elevationPainter.YOffset = profile.RenderSettings.AltitudeYOffset;
            _semiTransparentStepPainter.Multiplier = profile.RenderSettings.SemiTransparentStepIntensity;
        }
    }
}
