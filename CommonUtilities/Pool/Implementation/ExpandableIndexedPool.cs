namespace CommonUtilities.Pool
{
    /// <summary>
    /// Hands out one object per slot index, growing on demand.
    /// </summary>
    /// <remarks>
    /// <see cref="Provide"/> is called from every scanning thread, so growth publishes a new
    /// array rather than mutating a list in place: the previous version read Count and the
    /// indexer outside the lock that growth held, which could observe a list mid-resize.
    /// In practice it never grew, because callers pre-sized it to the slot count - but that
    /// made raising the parallelism silently unsafe.
    /// </remarks>
    public class ExpandableIndexedPool<TObject> : IPool<int, TObject>
    {
        public Func<int, TObject> ObjectCreator { get; set; }

        private TObject[] _pool;
        private readonly object _lock = new();

        public ExpandableIndexedPool(int capacity, Func<int, TObject> objectCreator)
        {
            ObjectCreator = objectCreator;

            _pool = new TObject[Math.Max(0, capacity)];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = objectCreator(i);
            }
        }

        public TObject Provide(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            TObject[] pool = Volatile.Read(ref _pool);
            if (index < pool.Length) return pool[index];

            return Grow(index);
        }

        private TObject Grow(int index)
        {
            lock (_lock)
            {
                TObject[] current = _pool;
                if (index < current.Length) return current[index];

                TObject[] grown = new TObject[index + 1];
                Array.Copy(current, grown, current.Length);

                for (int i = current.Length; i < grown.Length; i++)
                {
                    grown[i] = ObjectCreator(i);
                }

                // Publish only once fully built, so readers never see a partial array.
                Volatile.Write(ref _pool, grown);
                return grown[index];
            }
        }
    }
}
