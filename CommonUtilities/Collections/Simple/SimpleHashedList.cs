using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace CommonUtilities.Collections.Simple
{
    /// <summary>
    /// Insertion-ordered key/value list resolved by linear key equality. NBT compounds - the
    /// only user - hold a handful of short-string entries, so a scan with the reference check
    /// inside <c>string</c> equality beats hashing every inserted key: Add no longer walks
    /// the key's characters at all, and lookups match exactly instead of by hash code alone.
    /// </summary>
    public class SimpleHashedList<T, U> : IEnumerable<KeyValueEntry<T, U>> where T : notnull
    {
        private readonly SimpleList<KeyValueEntry<T, U>> _internalList;

        public ushort Count => (ushort)_internalList.Count;

        public U? this[T key]
        {
            get
            {
                TryGetValue(key, out U? value);
                return value;
            }
            set
            {
                if(value is null) throw new ArgumentNullException(nameof(value));
                SetValue(key, value);
            }
        }

        public SimpleHashedList(int count = 0)
        {
            _internalList = new SimpleList<KeyValueEntry<T, U>>(count);
        }

        public void Add(T key, U value)
        {
            _internalList.Add(new KeyValueEntry<T, U>(key, value, 0));
        }
        public void Clear()
        {
            _internalList.Clear();
        }
        public void ClearInternalArray()
        {
            _internalList.ClearInternalArray();
        }

        public void Remove(T key)
        {
            for (int i = 0; i < _internalList.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(_internalList[i].Key, key)) continue;

                _internalList.RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// Entry at a position in insertion order. Prefer this over LINQ ElementAt in loops:
        /// ElementAt allocates an enumerator and walks the list every call.
        /// </summary>
        public KeyValueEntry<T, U> EntryAt(int index)
        {
            return _internalList[index];
        }

        public bool TryGetValue(T key, [MaybeNullWhen(false)] out U value)
        {
            for (int i = 0; i < Count; i++) {
                if (!EqualityComparer<T>.Default.Equals(_internalList[i].Key, key)) continue;

                value = _internalList[i].Value;
                return true;
            }

            value = default;
            return false;
        }

        private void SetValue(T key, U value)
        {
            for (int i = 0; i < Count; i++) {
                if (!EqualityComparer<T>.Default.Equals(_internalList[i].Key, key)) continue;

                _internalList[i] = new (_internalList[i].Key, value, 0);
            }
        }

        public IEnumerator<KeyValueEntry<T, U>> GetEnumerator()
        {
            return _internalList.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _internalList.GetEnumerator();
        }
    }
}
