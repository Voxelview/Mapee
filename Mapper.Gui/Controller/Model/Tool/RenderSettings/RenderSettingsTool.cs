using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System.Windows;

namespace Mapper.Gui.Controller
{
    public class RenderSettingsTool : ToggleableTool
    {
        public Scene Scene { get; set; }

        private bool _isActive = false;
        private Window? _window;

        public RenderSettingsTool(Scene scene)
        {
            Scene = scene;
            Enabled = Scene.Domain.CurrentWorld is not null;
            
            Scene.WorldBeginChange += Scene_WorldBeginChange;
            Scene.WorldChanged += Scene_WorldChanged;
            Scene.DimensionBeginChange += Scene_DimensionBeginChange;
            Scene.DimensionChanged += Scene_DimensionChanged;
            Scene.StyleBeginReset += Scene_StyleReset;

            OnTurnedOn += TurnedOn;
            Application.Current.MainWindow.Closing += (sender, e) => Close();
        }

        private void Scene_WorldBeginChange(WorldDomain? old, WorldDomain current)
        {
            Close();
        }
        private void Scene_WorldChanged(WorldDomain? old, WorldDomain current) 
        {
            Enabled = Scene.Domain.CurrentWorld is not null;
        }

        private void Scene_DimensionBeginChange(DimensionDomain old, DimensionDomain current) 
        {
            Close();
        }
        private void Scene_DimensionChanged(DimensionDomain old, DimensionDomain current) 
        {
            Close();
        }
        private void Scene_StyleReset(Logic.Style old, Logic.Style current)
        {
            Close();
        }

        private void TurnedOn(bool isTurnedOn)
        {
            if (_isActive) 
            {
                if (IsTurnedOn) IsTurnedOn = false;
                else _window?.Activate();

                return;
            }

            _isActive = true;
            IsTurnedOn = false;

            OpenWindow();
        }

        private void OpenWindow()
        {
            if (Scene.Domain.CurrentWorld is null) return;

            _isActive = true;

            // GetProfile is the style's own per-dimension lookup with its default as the fallback,
            // and nothing outside RenderSettingsReader ever writes to what it reads - so this is
            // still the shipped profile however far the user has moved the live one.
            Model.RenderSettings defaultSettings = Scene.Domain.CurrentWorld.Style
                .GetProfile(Scene.Domain.CurrentWorld.CurrentDimension.Dimension).RenderSettings;

            RenderSettingsControl window = new(
                Scene.Domain.CurrentWorld.CurrentDimension.RenderSettings,
                defaultSettings,
                Scene.Domain.CurrentWorld.CurrentDimension.Dimension.Name);
            _window = window;
            window.Show();

            window.RenderProfileUpdated += (sender, e) => 
            {
                Profile profile = new(
                    Scene.Domain.CurrentWorld.Style.AssetPack,
                    Scene.Domain.CurrentWorld.CurrentDimension.HeightmapSettings,
                    window.RenderSettings);

                Scene.UpdateProfile(profile);
            };
            window.Closing += (sender, e) => _isActive = false;
        }
        private void Close() 
        {
            if(_isActive) _window?.Close();
        }
    }
}
