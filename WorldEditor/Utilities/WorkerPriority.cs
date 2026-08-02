namespace WorldEditor
{
    /// <summary>
    /// Drops the current thread to below-normal priority for the duration of pipeline work.
    /// A large load keeps every core busy, and at normal priority those workers compete
    /// with the UI thread for time slices - that read as sluggish buttons during loads.
    /// Below normal, the OS always schedules the UI first and the load still consumes
    /// every idle cycle.
    /// </summary>
    /// <remarks>
    /// Apply once per parallel-loop worker (localInit/localFinally), not per item: each
    /// priority change is a syscall. Restoring matters because thread-pool threads outlive
    /// the loop and go on to run unrelated work.
    /// </remarks>
    public static class WorkerPriority
    {
        public static ThreadPriority Lower()
        {
            Thread thread = Thread.CurrentThread;
            ThreadPriority previous = thread.Priority;
            if (previous != ThreadPriority.BelowNormal)
            {
                thread.Priority = ThreadPriority.BelowNormal;
            }

            return previous;
        }

        public static void Restore(ThreadPriority previous)
        {
            if (previous != ThreadPriority.BelowNormal)
            {
                Thread.CurrentThread.Priority = previous;
            }
        }
    }
}
