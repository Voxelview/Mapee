using System.Collections.Concurrent;

namespace WorldEditor
{
    public static class ParallelUtilities
    {
        /// <summary>
        /// Runs <paramref name="body"/> over [<paramref name="from"/>, <paramref name="toExclusive"/>)
        /// with at most <paramref name="buffer"/> iterations running at once.
        /// </summary>
        /// <remarks>
        /// Every running iteration is handed a slot in [0, <paramref name="buffer"/>) that no other
        /// running iteration holds, so the body can index per-slot pools without synchronizing.
        /// Slots are recycled as workers finish, so unlike a chunked loop there is no barrier between
        /// groups: a fast iteration never waits on a slow sibling.
        /// </remarks>
        public static void BufferedFor(int from, int toExclusive, int buffer, Action<int, int> body)
        {
            if (buffer <= 1)
            {
                for (int i = from; i < toExclusive; i++) body(i, 0);
                return;
            }

            ConcurrentStack<int> slots = new();
            for (int i = buffer - 1; i >= 0; i--) slots.Push(i);

            ParallelOptions options = new() { MaxDegreeOfParallelism = buffer };

            Parallel.For(from, toExclusive, options,
                () => new WorkerState(TakeSlot(slots), WorkerPriority.Lower()),
                (index, _, state) =>
                {
                    body(index, state.Slot);
                    return state;
                },
                state =>
                {
                    slots.Push(state.Slot);
                    WorkerPriority.Restore(state.Previous);
                });
        }

        private readonly record struct WorkerState(int Slot, ThreadPriority Previous);

        /// <summary>
        /// MaxDegreeOfParallelism caps how many workers hold a slot at once, so the stack is never
        /// actually empty here. Spinning rather than falling back to a shared slot keeps that an
        /// assumption we can't silently violate: two workers on one slot would corrupt pooled buffers.
        /// </summary>
        private static int TakeSlot(ConcurrentStack<int> slots)
        {
            SpinWait spin = default;
            int slot;
            while (!slots.TryPop(out slot)) spin.SpinOnce();
            return slot;
        }
    }
}
