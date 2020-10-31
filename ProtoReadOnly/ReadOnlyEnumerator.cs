using System.Collections;
using System.Collections.Generic;

namespace ProtoReadOnly
{
    public class ReadOnlyEnumerator<K, IV, OV> : IEnumerator<KeyValuePair<K, OV>> where IV : OV
    {
        private IEnumerator<KeyValuePair<K, IV>> original;

        public ReadOnlyEnumerator(IEnumerator<KeyValuePair<K, IV>> original)
        {
            this.original = original;
        }

        public KeyValuePair<K, OV> Current
        {
            get
            {
                var current = original.Current;
                return KeyValuePair.Create<K, OV>(current.Key, current.Value);
            }
        }

        object IEnumerator.Current => original.Current;

        public void Dispose()
        {
            original.Dispose();
        }

        public bool MoveNext()
        {
            return original.MoveNext();
        }

        public void Reset()
        {
            original.Reset();
        }
    }
}
