// =============================================================================
// SerializableDict<TKey,TValue> — a Unity-serializable Dictionary
// -----------------------------------------------------------------------------
// Unity's built-in serializer (and the SO inspector) cannot serialize
// Dictionary<,>. The save FILE uses Newtonsoft (which handles dictionaries
// natively), but the live GameState SO must also survive an editor domain
// reload — so dictionary-typed SO fields use this helper: parallel key/value
// Lists synced via ISerializationCallbackReceiver.
//
// It IS a Dictionary<TKey,TValue> (subclass), so it behaves like one for all
// reads/writes and Newtonsoft serializes it as a plain JSON object — the save
// file shape is identical to the React `Record<string,T>`.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.State
{
    /// <summary>A <see cref="Dictionary{TKey,TValue}"/> that Unity can serialize.</summary>
    [Serializable]
    public class SerializableDict<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> _keys = new List<TKey>();
        [SerializeField] private List<TValue> _values = new List<TValue>();

        public SerializableDict() { }

        public SerializableDict(IDictionary<TKey, TValue> source) : base(source) { }

        /// <summary>Flatten the dictionary into the parallel lists before Unity writes it.</summary>
        public void OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();
            foreach (var kv in this)
            {
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }
        }

        /// <summary>Rebuild the dictionary from the parallel lists after Unity reads it.</summary>
        public void OnAfterDeserialize()
        {
            Clear();
            var n = Math.Min(_keys.Count, _values.Count);
            for (var i = 0; i < n; i++)
            {
                if (_keys[i] != null && !ContainsKey(_keys[i]))
                    Add(_keys[i], _values[i]);
            }
        }
    }
}
