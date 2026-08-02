namespace AssetSystem
{
    public static class PropertyValueProviderUtilities
    {
        /// <summary>
        /// One reusable getter per thread. The delegate is only ever invoked synchronously
        /// inside the matcher call that requested it, and allocating a fresh closure here
        /// happened for every property-carrying block lookup of a load.
        /// </summary>
        [ThreadStatic]
        private static Holder? t_holder;

        private sealed class Holder
        {
            public WorldEditor.Property[] Properties = System.Array.Empty<WorldEditor.Property>();
            public readonly PropertyValueProvider Provider;

            public Holder()
            {
                Provider = Get;
            }

            private string Get(string name)
            {
                WorldEditor.Property[] properties = Properties;
                for (int i = 0; i < properties.Length; i++)
                {
                    if (properties[i].Name == name) return properties[i].Value;
                }

                return string.Empty;
            }
        }

        public static PropertyValueProvider CreateGetter(WorldEditor.Property[] properties)
        {
            Holder holder = t_holder ??= new Holder();
            holder.Properties = properties;

            return holder.Provider;
        }
    }
}
