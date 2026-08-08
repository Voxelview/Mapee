using Mapper.Gui.Model;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// The rail's primary button. It no longer opens anything of its own - IsTurnedOn now means,
    /// exactly, "the Atlas is up".
    ///
    /// That flip from momentary to genuinely toggleable is the point: the button used to be
    /// flicked on and straight back off around a blocking ShowDialog, so it flashed and went dark
    /// while a modal dialog sat in front of it. Held, the rail button lights amber for as long as
    /// the picker is open and a second click puts it away - which costs nothing, because
    /// ToolButtonHook already paints every state of a latched tool.
    /// </summary>
    public class BrowseTool : ToggleableTool
    {
        public IAtlasWidget Atlas { get; }
        public MapViewer MapViewer { get; }

        public BrowseTool(IAtlasWidget atlas, MapViewer mapViewer)
        {
            Atlas = atlas;
            MapViewer = mapViewer;

            OnTurnedOn += isTurnedOn =>
            {
                if (isTurnedOn) Atlas.Open();
                else Atlas.Close();
            };

            // The other direction: Escape, the close button, a click on the scrim and picking a
            // world all close the Atlas without coming through this button. ToggleableTool's
            // setter drops a write of the value it already holds, so this cannot loop.
            Atlas.OpenChanged += (sender, isOpen) => IsTurnedOn = isOpen;

            // The picker still comes up by itself the first time the window paints - opening a
            // world is the one thing you do before anything else here works.
            if (MapViewer.HasBeenShown) IsTurnedOn = true;
            else MapViewer.Shown += (sender, e) => IsTurnedOn = true;
        }
    }
}
