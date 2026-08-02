namespace WorldEditor
{
    public interface IChunkEnumerator
    {
        void Enumerate(Coords[] regions, IEnumerationBody body);

        /// <summary>
        /// Streams regions through the reader slots until <paramref name="takeNext"/> comes
        /// back empty. A slot picks up its next region the moment it finishes the previous
        /// one, so no slot ever waits on a sibling; the whole call is one cycle of
        /// <paramref name="body"/>.
        /// </summary>
        /// <param name="takeNext">
        /// Called from multiple slots concurrently; must hand out each region at most once.
        /// Empty (or null) result stops the slot that received it.
        /// </param>
        void Enumerate(Func<Coords[]> takeNext, IEnumerationBody body);
    }
}
