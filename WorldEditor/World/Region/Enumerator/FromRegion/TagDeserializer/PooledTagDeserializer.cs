using NbtEditor;
using CommonUtilities.Pool;
using System.Buffers;
using System.Collections.Frozen;

namespace WorldEditor
{
    public class PooledTagDeserializer : ITagDeserializer
    {
        /// <summary>
        /// Chunk subtrees no reader in the mapping pipeline ever touches. They are most of a
        /// modern chunk's tag count (block entities, tick queues, structure data), so skipping
        /// them cuts most of the deserialization work and pooled-tag churn per chunk.
        /// </summary>
        private static readonly FrozenSet<string> ChunkSkipNames = new[]
        {
            // 1.18+ chunk root
            "block_entities", "block_ticks", "fluid_ticks", "PostProcessing", "structures",
            "blending_data", "below_zero_retrogen", "isLightOn", "InhabitedTime", "LastUpdate",
            // pre-1.18 "Level" children
            "Entities", "TileEntities", "TileTicks", "LiquidTicks", "ToBeTicked",
            "LiquidsToBeTicked", "Structures", "CarvingMasks", "Lights", "UpgradeData",
            // entity storage inside region chunks of very old versions
            "entities",
        }.ToFrozenSet();

        public virtual NbtReader[] NbtReaders { get; set; }

        public virtual TagDeserializer[] TagDeserializers { get; set; }
        public virtual PooledValueTagAllocation[] ValueTagAllocations { get; set; }
        public virtual PooledArrayTagAllocation[] ArrayTagAllocations { get; set; }
        public virtual IResettablePool<int, ListTag>[] ListPools { get; set; }
        public virtual IResettablePool<CompoundTag>[] CompoundPools { get; set; }

        private IResettablePool<int, sbyte[]>[] _sbytePools;
        private IResettablePool<int, int[]>[] _intPools;
        private IResettablePool<int, long[]>[] _longPools;

        public PooledTagDeserializer(int tasks)
        {
            NbtReaders = new NbtReader[tasks];
            _sbytePools = new ResettableCachedPool<sbyte>[tasks];
            _intPools = new ResettableCachedPool<int>[tasks];
            _longPools = new ResettableCachedPool<long>[tasks];

            for (int i = 0; i < NbtReaders.Length; i++)
            {
                _sbytePools[i] = new ResettableCachedPool<sbyte>();
                _intPools[i] = new ResettableCachedPool<int>();
                _longPools[i] = new ResettableCachedPool<long>();

                NbtReaders[i] = new NbtReader(new FastBufferProvider(Array.Empty<byte>()), new ArrayPoolAllocation()
                {
                    SBytePool = _sbytePools[i],
                    Int32Pool = _intPools[i],
                    Int64Pool = _longPools[i]
                });
            }

            TagDeserializers = new TagDeserializer[tasks];
            ValueTagAllocations = new PooledValueTagAllocation[tasks];
            ArrayTagAllocations = new PooledArrayTagAllocation[tasks];
            ListPools = new IResettablePool<int, ListTag>[tasks];
            CompoundPools = new IResettablePool<CompoundTag>[tasks];

            for (int i = 0; i < TagDeserializers.Length; i++)
            {
                PooledValueTagAllocation valueTagAllocation = new PooledValueTagAllocation();
                PooledArrayTagAllocation arrayTagAllocation = new PooledArrayTagAllocation();
                IResettablePool<CompoundTag> compoundTagPool = new ExpandableObjectPool<CompoundTag>(0, () => new CompoundTag());
                IResettablePool<int, ListTag> listTagPool = new ExpandableObjectPool<int, ListTag>(0, count => new ListTag(TagId.End));

                IdTagDeserializer idTagDeserializer = new(
                    valueTagAllocation,
                    arrayTagAllocation,
                    listTagPool,
                    compoundTagPool
                );
                if (idTagDeserializer.CompoundTagDeserializer is CompoundTagDeserializer compoundTagDeserializer)
                {
                    compoundTagDeserializer.SkipNames = ChunkSkipNames;
                }

                TagDeserializers[i] = new TagDeserializer()
                {
                    IdTagDeserializer = idTagDeserializer
                };

                ValueTagAllocations[i] = valueTagAllocation;
                ArrayTagAllocations[i] = arrayTagAllocation;
                ListPools[i] = listTagPool;
                CompoundPools[i] = compoundTagPool;
            }
        }

        public virtual CompoundTag? Deserialize(ArraySlice<byte> decompressed, int iterator)
        {
            NbtReader reader = NbtReaders[iterator];
            reader.BufferProvider = new FastBufferProvider(decompressed.Array, decompressed.Position);

            Tag tag = TagDeserializers[iterator].Deserialize(reader);

            ValueTagAllocations[iterator].Reset();
            ArrayTagAllocations[iterator].Reset();
            ListPools[iterator].Reset();
            CompoundPools[iterator].Reset();

            _sbytePools[iterator].Reset();
            _intPools[iterator].Reset();
            _longPools[iterator].Reset();

            if (tag is not CompoundTag compoundTag) return null;
            return compoundTag;
        }
    }

    /// <summary>
    /// Array pool private to one deserialization slot, so no call ever synchronizes.
    /// <see cref="ArrayPool{T}.Shared"/> here meant every chunk of every worker fought over
    /// the shared per-core stacks - lock contention plus a tracking set allocation per chunk.
    /// </summary>
    /// <remarks>
    /// Keeps the deferred-reset contract of the pool it replaces: the first
    /// <see cref="Reset"/> only arms, and arrays are actually reclaimed on the next
    /// <see cref="Provide(int)"/>, because the tag tree produced by a chunk's deserialization
    /// is still being consumed when Reset is called and must stay valid until the slot
    /// starts its next chunk.
    /// </remarks>
    public class ResettableCachedPool<T> : IResettablePool<int, T[]>
    {
        /// <summary>Free arrays kept per power-of-two size class, newest first.</summary>
        private const int Buckets = 31;
        private const int MinLength = 16;
        private const int MaxFreePerBucket = 128;

        private readonly T[]?[][] _free = new T[Buckets][][];
        private readonly int[] _freeCount = new int[Buckets];

        private T[]?[] _lent = new T[64][];
        private int _lentCount;

        private bool _resetPending = false;

        public T[] Provide(int length)
        {
            if (_resetPending) Reset();

            int bucket = BucketOf(length);
            T[]? output = null;

            if (_freeCount[bucket] > 0)
            {
                output = _free[bucket][--_freeCount[bucket]];
                _free[bucket][_freeCount[bucket]] = null;
            }
            output ??= new T[Math.Max(length, 1 << bucket)];

            if (_lentCount == _lent.Length) Array.Resize(ref _lent, _lentCount * 2);
            _lent[_lentCount++] = output;

            return output;
        }

        public void Reset()
        {
            if (!_resetPending)
            {
                _resetPending = true;
                return;
            }

            _resetPending = false;

            for (int i = 0; i < _lentCount; i++)
            {
                T[] array = _lent[i]!;
                _lent[i] = null;

                int bucket = BucketOf(array.Length);
                if (_freeCount[bucket] >= MaxFreePerBucket) continue;

                _free[bucket] ??= new T[MaxFreePerBucket][];
                _free[bucket][_freeCount[bucket]++] = array;
            }

            _lentCount = 0;
        }

        private static int BucketOf(int length)
        {
            if (length < MinLength) length = MinLength;
            return 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)(length - 1));
        }
    }
}
