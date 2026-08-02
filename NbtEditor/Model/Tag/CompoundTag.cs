using CommonUtilities.Collections.Simple;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NbtEditor 
{
    public sealed class CompoundTag : Tag, IEnumerable<KeyValueEntry<string, Tag>>
    {
        private SimpleHashedList<string, Tag> _tags;

        public int Count => _tags.Count;

        public Tag? this[string key]
        {
            get => _tags[key];
            set => _tags[key] = value;
        }

        public CompoundTag() : base(TagId.Compound) 
        {
            _tags = new SimpleHashedList<string, Tag>();
        }
        public CompoundTag(int count) : base(TagId.Compound) 
        {
            _tags = new SimpleHashedList<string, Tag>(count);
        }

        public void Add(string key, Tag value) 
        {
            _tags.Add(key, value);
        }
        public void Add(KeyValueEntry<string, Tag> item)
        {
            _tags.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _tags.Clear();
        }
        public void ClearInternalArray() 
        {
            _tags.ClearInternalArray();
        }

        public void Remove(string key) 
        {
            _tags.Remove(key);
        }

        /// <summary>Entry at a position in insertion order, without enumerator allocation.</summary>
        public KeyValueEntry<string, Tag> EntryAt(int index)
        {
            return _tags.EntryAt(index);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out Tag value)
        {
            return _tags.TryGetValue(key, out value);
        }

        public Tag GetChild(string path)
        {
            return GetChild(path.Split("/"));
        }
        public Tag GetChild(params string[] path)
        {
            CompoundTag current = this;
            for (int i = 0; i < path.Length - 1; i++)
            {
                current = (CompoundTag)current._tags[path[i]];
            }

            return current._tags[path[^1]];
        }

        public bool TryGetChild(string path, out Tag child)
        {
            return TryGetChild(out child, path.Split("/"));
        }
        public bool TryGetChild(out Tag child, params string[] path)
        {
            CompoundTag current = this;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (!current._tags.TryGetValue(path[i], out Tag tag) || tag is not CompoundTag c)
                {
                    child = null;
                    return false;
                }
                current = c;
            }

            return current._tags.TryGetValue(path[^1], out child);
        }

        /// <summary>Two-level lookup without the params-array allocation of the array overload.</summary>
        public bool TryGetChild(string first, string second, out Tag child)
        {
            if (_tags.TryGetValue(first, out Tag tag) && tag is CompoundTag c)
            {
                return c._tags.TryGetValue(second, out child);
            }

            child = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _tags.GetEnumerator();
        }
        public IEnumerator<KeyValueEntry<string, Tag>> GetEnumerator() 
        {
            return _tags.GetEnumerator();
        }
    }
}
