using Mapper.Gui.Model;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Windows.Media;
using WorldEditor;

namespace Mapper.Gui.Logic
{
    public class RegionLoader : IRegionLoader
    {
        public Scene Scene { get; }
        public WorldMapper WorldMapper { get; }
        public ILoadPattern LoadPattern { get; set; }

        public XzRange LoadedArea => _currentScene is null ? XzRange.Empty : new(_currentScene.TopLeft, _currentScene.BottomRight);

        private readonly IDictionary<Dimension, SceneCache> _cachedScenes;
        private SceneCache? _currentScene;
        private bool _inSceneChangingProcess = false;
        private object _lock = new();

        public RegionLoader(Scene scene)
        {
            Scene = scene;
            WorldMapper = new WorldMapper(Scene.Domain.ChunkMapperPack);
            LoadPattern = new SpiralLoadPattern();
            _cachedScenes = new Dictionary<Dimension, SceneCache>();

            if (Scene.Domain.CurrentWorld is not null)
            {
                SetCurrentScene(Scene.Domain.CurrentWorld, Scene.Domain.CurrentWorld.CurrentDimension);
            }

            WorldMapper.RegionRendered += OnRegionRendered;
            Scene.WorldBeginChange += Scene_WorldBeginChange;
            Scene.DimensionBeginChange += Scene_DimensionBeginChange;
            Scene.WorldChanged += Scene_WorldChanged;
            Scene.DimensionChanged += Scene_DimensionChanged;
            Scene.StyleReset += Scene_StyleReset;
            Scene.ProfileReset += Scene_ProfileReset;
        }

        private void Scene_WorldBeginChange(WorldDomain? old, WorldDomain current)
        {
            if (old is not null)
            {
                _inSceneChangingProcess = true;
                WorldMapper.Invoke(WorldMapper.Stop);

                _cachedScenes.Clear();
                _currentScene = null;
            }

            SetCurrentScene(current, current.CurrentDimension);
        }
        private void Scene_DimensionBeginChange(DimensionDomain old, DimensionDomain current)
        {
            _inSceneChangingProcess = true;
            WorldMapper.Invoke(WorldMapper.Stop);

            if (Scene.Domain.CurrentWorld is null) return;
            SetCurrentScene(Scene.Domain.CurrentWorld, current);
        }
        private void Scene_WorldChanged(WorldDomain? old, WorldDomain current)
        {
            WorldMapper.Invoke(() =>
            {
                WorldMapper.SetScene(current.CurrentDimension.Scene.SceneParameter);
                _inSceneChangingProcess = false;
            });
        }
        private void Scene_DimensionChanged(DimensionDomain old, DimensionDomain current)
        {
            WorldMapper.Invoke(() =>
            {
                WorldMapper.SetScene(current.Scene.SceneParameter);
                _inSceneChangingProcess = false;
            });
        }

        private void Scene_StyleReset(Style old, Style current)
        {
            if (Scene.Domain.CurrentWorld is null) return;

            foreach (DimensionDomain domain in Scene.Domain.CurrentWorld.Dimensions)
            {
                domain.Scene.RenderedRegions.Clear();
            }
            _cachedScenes.Clear();
        }
        private void Scene_ProfileReset(DimensionDomain dimension)
        {
            if (_currentScene is null || Scene.Domain.CurrentWorld is null) return;

            _inSceneChangingProcess = true;

            SetCurrentScene(Scene.Domain.CurrentWorld, dimension);
            _currentScene.Reset();

            WorldMapper.Invoke(() =>
            {
                dimension.Scene.RenderedRegions.Clear();
                WorldMapper.SetScene(dimension.Scene.SceneParameter);
                _inSceneChangingProcess = false;
            });
        }

        private void SetCurrentScene(WorldDomain world, DimensionDomain dimension)
        {
            if (!_cachedScenes.TryGetValue(dimension.Dimension, out SceneCache? scene) || scene is null)
            {
                scene = new SceneCache();
                _cachedScenes.Add(dimension.Dimension, scene);
            }

            Profile profile = new(world.Style.AssetPack, dimension.HeightmapSettings, dimension.RenderSettings);
            Scene.Domain.ChunkMapperPack.UpdatePack(profile, world.Style.ScanType);

            scene.PrevRange = XzRange.Empty;
            _currentScene = scene;
        }

        private void OnRegionRendered(Coords coords, ICanvas canvas)
        {
            if (_currentScene is null || Scene.Domain.CurrentWorld is null || _inSceneChangingProcess) return;
            if (!IsSafeToAdd(coords.X, coords.Z)) return;

            ImageSource? bitmap = canvas.GetBitmap();
            if (bitmap is not null)
            {
                Coords topLeft = new(canvas.TopLeftPoint.X - ((int)bitmap.Width - 1), canvas.TopLeftPoint.Z);
                Coords bottomRight = new(topLeft.X + (int)bitmap.Width - 1, topLeft.Z + (int)bitmap.Height - 1);

                if (topLeft.X < _currentScene.TopLeft.X) _currentScene.TopLeft = new XzPoint(topLeft.X, _currentScene.TopLeft.Z);
                if (topLeft.Z < _currentScene.TopLeft.Z) _currentScene.TopLeft = new XzPoint(_currentScene.TopLeft.X, topLeft.Z);

                if (bottomRight.X > _currentScene.BottomRight.X) _currentScene.BottomRight = new XzPoint(bottomRight.X, _currentScene.BottomRight.Z);
                if (bottomRight.Z > _currentScene.BottomRight.Z) _currentScene.BottomRight = new XzPoint(_currentScene.BottomRight.X, bottomRight.Z);

                XzRange areaInRegion = new(
                    MathUtilities.NegMod(topLeft.X, 512),
                    MathUtilities.NegMod(topLeft.Z, 512),
                    (int)bitmap.Width - 1,
                    (int)bitmap.Height - 1);

                RenderedRegion output = new(new XzPoint(coords.X, coords.Z), areaInRegion, bitmap);
                AddSafely(output);
            }
            else
            {
                AddSafely(new RenderedRegion(new XzPoint(coords.X, coords.Z), null, null));
            }
        }

        private bool IsSafeToAdd(int x, int z)
        {
            if(Scene.Domain.CurrentWorld is null) return false;

            lock (_lock)
            {
                return !Scene.Domain.CurrentWorld.CurrentDimension.Scene.RenderedRegions.ContainsKey(new XzPoint(x, z));
            }
        }
        private void AddSafely(IRenderedRegion region)
        {
            if (Scene.Domain.CurrentWorld is null) return;

            lock (_lock)
            {
                if (Scene.Domain.CurrentWorld.CurrentDimension.Scene.RenderedRegions.ContainsKey(region.Coords)) return;
                Scene.Domain.CurrentWorld.CurrentDimension.Scene.RenderedRegions.Add(region.Coords, region);
            }
        }

        public void LoadArea(XzRange range)
        {
            if (_currentScene is null || range == _currentScene?.PrevRange || _inSceneChangingProcess) return;

            WorldMapper.Invoke(() =>
            {
                Coords[] queue = ArrayPool<Coords>.Shared.Rent((int)range.Size.X * (int)range.Size.Z);
                Memory<Coords> queueMemory = queue;
                CreateQueue(range, ref queueMemory);

                WorldMapper.Queue.ReplaceWith(queueMemory);
                ArrayPool<Coords>.Shared.Return(queue);
            });

            if (_currentScene is not null) _currentScene.PrevRange = range;
        }
        private void CreateQueue(XzRange range, ref Memory<Coords> queue)
        {
            if (Scene.Domain.CurrentWorld is null) return;
            IRenderedScene renderedScene = Scene.Domain.CurrentWorld.CurrentDimension.Scene;

            int index = 0;
            Span<Coords> queueSpan = queue.Span;
            foreach (XzPoint regionPoint in LoadPattern.CreatePattern(range))
            {
                Coords regionCoords = regionPoint.ToCoords();

                if (!renderedScene.SceneParameter.RegionStore.Exists(regionCoords))
                {
                    continue;
                }
                if (renderedScene.RenderedRegions.ContainsKey(new XzPoint(regionCoords.X, regionCoords.Z)))
                {
                    continue;
                }

                queueSpan[index++] = regionCoords;
            }

            queue = queue[..index];
        }
    }
}