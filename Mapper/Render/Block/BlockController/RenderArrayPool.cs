namespace Mapper
{
    /// <summary>
    /// Thread-local array pool for the small per-section render tables. Sections are built
    /// and disposed on the same render thread, and the shared <see cref="System.Buffers.ArrayPool{T}"/>
    /// made every rent/return a synchronized operation across all render workers - the
    /// returns alone were several percent of a large load.
    /// </summary>
    internal static class RenderArrayPool<T>
    {
        private const int Buckets = 16;
        private const int MaxPerBucket = 64;

        [ThreadStatic]
        private static Stack<T[]>?[]? t_free;

        public static T[] Rent(int minimumLength)
        {
            int bucket = BucketOf(minimumLength);

            Stack<T[]>?[] free = t_free ??= new Stack<T[]>?[Buckets];
            Stack<T[]>? stack = bucket < Buckets ? free[bucket] : null;

            if (stack is not null && stack.Count > 0) return stack.Pop();
            return new T[1 << bucket];
        }

        public static void Return(T[] array)
        {
            int bucket = System.Numerics.BitOperations.Log2((uint)array.Length);
            if (bucket >= Buckets) return;

            Stack<T[]>?[] free = t_free ??= new Stack<T[]>?[Buckets];
            Stack<T[]> stack = free[bucket] ??= new Stack<T[]>();

            if (stack.Count < MaxPerBucket) stack.Push(array);
        }

        private static int BucketOf(int length)
        {
            if (length < 2) length = 2;
            return 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)(length - 1));
        }
    }
}
