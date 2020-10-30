using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;

namespace ProtoReadOnly
{
    public class ReadOnlyWrapper<K, IV, OV> : IReadOnlyDictionary<K, OV> where IV : OV
    {
        private MapField<K, IV> original;

        public ReadOnlyWrapper(MapField<K, IV> original)
        {
            this.original = original;
        }

        public OV this[K key] => original[key];

        public IEnumerable<K> Keys => original.Keys;

        public IEnumerable<OV> Values => original.Values.Cast<OV>();

        public int Count => original.Count;

        public bool ContainsKey(K key)
        {
            return original.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<K, OV>> GetEnumerator()
        {
            return new ReadOnlyEnumerator<K, IV, OV>(original.GetEnumerator());
        }

        public bool TryGetValue(K key, out OV value)
        {
            IV firstOut;
            var result = original.TryGetValue(key, out firstOut);
            value = firstOut;
            return result;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return original.GetEnumerator();
        }
    }
}
