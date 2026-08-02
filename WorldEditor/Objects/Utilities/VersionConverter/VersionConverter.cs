namespace WorldEditor
{
    public abstract class VersionConverter : IVersionConverter
    {
        public IList<IInstanceConverter<IObject?>> Converters { get; set; }

        public VersionConverter()
        {
            Converters = new List<IInstanceConverter<IObject?>>();
            InitializeConverters();
        }

        /// <summary>
        /// Converter chains resolved per (from, to) pair. Every chunk of a world resolves the
        /// same pair, and this used to build a list + reverse it per object per chunk.
        /// Reference-typed entry so concurrent readers see a consistent pair/chain snapshot.
        /// </summary>
        private sealed record ResolvedChain(Version From, Version To, IInstanceConverter<IObject?>[] Chain);
        private ResolvedChain? _resolvedChain;

        public IObject? Convert(IObject input, Version from, Version to, UsageIntent intent)
        {
            IObject? output = input;

            foreach (IInstanceConverter<IObject?> converter in GetConverters(from, to))
            {
                if (output is null) return null;
                output = converter.Convert(output, intent);
            }

            return output;
        }
        protected virtual IReadOnlyList<IInstanceConverter<IObject?>> GetConverters(Version from, Version to)
        {
            ResolvedChain? resolved = _resolvedChain;
            if (resolved is not null && resolved.From == from && resolved.To == to) return resolved.Chain;

            List<IInstanceConverter<IObject?>> output = new();

            Version scanTo = to;
            for (int i = Converters.Count - 1; i >= 0; i--)
            {
                IInstanceConverter<IObject?> converter = Converters[i];
                if (!converter.To.IsInRange(scanTo)) continue;
                if (from > converter.From.End) continue;

                output.Add(converter);

                if (converter.From.IsInRange(from)) break;
                scanTo = converter.From.Start;
            }

            output.Reverse();

            IInstanceConverter<IObject?>[] chain = output.ToArray();
            _resolvedChain = new ResolvedChain(from, to, chain);
            return chain;
        }

        protected abstract void InitializeConverters();
    }
}
