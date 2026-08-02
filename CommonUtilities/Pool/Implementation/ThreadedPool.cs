namespace CommonUtilities.Pool
{
    /// <summary>
    /// The slot index the current thread is working on, used to hand out per-worker pool instances.
    /// </summary>
    /// <remarks>
    /// This replaces <c>Thread.GetNamedDataSlot</c>, which resolves the name through a process-wide
    /// locked table on every access. The scan path sets and reads this once per chunk, so on a large
    /// load that lock was being taken millions of times across every worker thread.
    /// </remarks>
    public static class ThreadSlot
    {
        [ThreadStatic]
        private static int _index;

        public static int Index
        {
            get => _index;
            set => _index = value;
        }
    }

    public class ThreadedPool<TOutput> : IPool<TOutput>
    {
        public IPool<int, TOutput> Pool { get; set; }

        public ThreadedPool(IPool<int, TOutput> pool)
        {
            Pool = pool;
        }

        public TOutput Provide()
        {
            return Pool.Provide(ThreadSlot.Index);
        }
    }
}
