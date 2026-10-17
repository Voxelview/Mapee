using Mapper.Gui.Logic;
using Mapper.Gui.Model;
using System;
using WorldEditor;

namespace Mapper.Gui.Controller
{
    public class BrowseTool : ToggleableTool
    {
        public Scene Scene { get; }
        public MapViewer MapViewer { get; }

        private bool _isActive = false;

        public BrowseTool(Scene scene, MapViewer mapViewer)
        {
            Scene = scene;
            MapViewer = mapViewer;

            if (MapViewer.HasBeenShown) IsTurnedOn = true;
            else MapViewer.Shown += (sender, e) => IsTurnedOn = true;

            OnTurnedOn += TurnedOn;
        }

        private void TurnedOn(bool isTurnedOn)
        {
            if (_isActive) return;

            _isActive = true;
            IsTurnedOn = false;

            OpenWindow();
        }

        private void OpenWindow()
        {
            _isActive = true;

            WorldBrowser browser = new(AnyEditionLevelReader.DefaultWorldDirectories(), new AnyEditionLevelReader());
            browser.ShowDialog();

            _isActive = false;

            if (browser.DialogClosed) return;
            Scene.SetWorld(browser.Selected.Level);
        }
    }
}
