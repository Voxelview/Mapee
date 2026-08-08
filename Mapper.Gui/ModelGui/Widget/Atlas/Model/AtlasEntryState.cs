namespace Mapper.Gui.Model
{
    public enum AtlasEntryState
    {
        /// <summary>
        /// On screen, but nothing has been read off disk yet beyond the folder name. A plate in
        /// this state is deliberately not clickable - there is no Level behind it to open.
        /// </summary>
        Pending,
        Ready,

        /// <summary>
        /// The folder is in the saves directory but has no level.dat we can read. Dropped from the
        /// list rather than shown, matching what the old browser did.
        /// </summary>
        Failed
    }
}
