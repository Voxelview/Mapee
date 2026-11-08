namespace WorldEditor
{
    public abstract class VersionConverter : IVersionConverter
    {
        public IList<IInstanceConverter<IObject?>> Converters { get; set; }

        public VersionConverter() : this(initialize: true)
        {
        }

        /// <summary>
        /// Pass <c>false</c> when the subclass needs to store its own settings first; it must then
        /// call <see cref="InitializeConverters"/> itself at the end of its constructor.
        /// </summary>
        protected VersionConverter(bool initialize)
        {
            Converters = new List<IInstanceConverter<IObject?>>();
            if (initialize) InitializeConverters();
        }

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
        protected virtual IEnumerable<IInstanceConverter<IObject?>> GetConverters(Version from, Version to)
        {
            List<IInstanceConverter<IObject?>> output = new();

            for (int i = Converters.Count - 1; i >= 0; i--)
            {
                IInstanceConverter<IObject?> converter = Converters[i];
                if (!converter.To.IsInRange(to)) continue;
                if (from > converter.From.End) continue;

                output.Add(converter);

                if (converter.From.IsInRange(from)) break;
                to = converter.From.Start;
            }

            output.Reverse();
            return output;
        }

        protected abstract void InitializeConverters();
    }
}
