using System;
using System.Collections.Generic;
using WorldEditor;

namespace MapScanner
{
    /// <summary>
    /// Maps a section's Y to its position in a section list, so pairing up the block,
    /// biome and light sections of a chunk costs one lookup each instead of a scan.
    /// </summary>
    /// <remarks>
    /// Entries are index+1; 0 means the chunk has no section at that Y. The map is 256
    /// wide because section Y is an <see cref="sbyte"/>, and is meant to be stackalloc'd
    /// by the caller - it is rebuilt per chunk, and per-chunk allocations are what this
    /// exists to avoid.
    /// </remarks>
    internal static class SectionIndex
    {
        public const int Size = 256;
        private const int Bias = 128;

        public static void Build<T>(IList<T>? sections, Span<short> map) where T : ISection
        {
            if (sections is null) return;

            for (int i = 0; i < sections.Count; i++)
            {
                map[sections[i].Y + Bias] = (short)(i + 1);
            }
        }

        public static T? Get<T>(IList<T>? sections, ReadOnlySpan<short> map, int y) where T : class, ISection
        {
            // Callers ask for y+1 to reach the section above, which can leave sbyte range
            // at the top of the world.
            if (sections is null || y < sbyte.MinValue || y > sbyte.MaxValue) return null;

            short index = map[y + Bias];
            return index == 0 ? null : sections[index - 1];
        }

        public static int IndexOf(ReadOnlySpan<short> map, int y)
        {
            if (y < sbyte.MinValue || y > sbyte.MaxValue) return -1;
            return map[y + Bias] - 1;
        }
    }
}
