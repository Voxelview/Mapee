using MapScanner;

namespace Mapper
{
    public readonly struct MapRenderArgs
    {
        /// <summary>Slot-per-chunk array from <c>ScannedRegion</c>; empty slots are null.</summary>
        public IScannedChunk?[] Chunks { get; }
        public IStepProvider StepProvider { get; }

        public MapRenderArgs(IScannedChunk?[] chunks, IStepProvider stepProvider)
        {
            Chunks = chunks;
            StepProvider = stepProvider;
        }
    }
}
