using System;
using System.Collections;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Groups values by key while preserving both first-key and per-key insertion
/// order. Generated database loaders retain their schema-specific parsing and
/// validation; this type only removes their repeated add-or-create plumbing.
/// </summary>
internal sealed class Lookup<TKey, TValue> :
    IEnumerable<KeyValuePair<TKey, IReadOnlyList<TValue>>>
    where TKey : notnull
{
    private readonly Dictionary<TKey, List<TValue>> _values;
    private readonly List<TKey> _keys = [];

    internal Lookup()
        : this(comparer: null)
    {
    }

    internal Lookup(IEqualityComparer<TKey>? comparer)
    {
        _values = new Dictionary<TKey, List<TValue>>(comparer);
    }

    internal int KeyCount => _keys.Count;
    internal int Count => KeyCount;
    internal IReadOnlyList<TKey> Keys => _keys;
    internal IReadOnlyList<TValue> this[TKey key] => _values[key];
    internal IEnumerable<IReadOnlyList<TValue>> Values
    {
        get
        {
            foreach (TKey key in _keys)
                yield return _values[key];
        }
    }

    internal void Add(TKey key, TValue value) => GetOrAdd(key).Add(value);

    internal List<TValue> GetOrAdd(TKey key)
    {
        if (_values.TryGetValue(key, out List<TValue>? values))
            return values;

        values = [];
        _values.Add(key, values);
        _keys.Add(key);
        return values;
    }

    internal bool TryGetValues(TKey key, out IReadOnlyList<TValue> values)
    {
        if (_values.TryGetValue(key, out List<TValue>? found))
        {
            values = found;
            return true;
        }

        values = Array.Empty<TValue>();
        return false;
    }

    internal IReadOnlyList<TValue> ValuesOrEmpty(TKey key) =>
        _values.TryGetValue(key, out List<TValue>? values)
            ? values
            : Array.Empty<TValue>();

    internal void SortValues(Comparison<TValue> comparison)
    {
        foreach (TKey key in _keys)
            _values[key].Sort(comparison);
    }

    public IEnumerator<KeyValuePair<TKey, IReadOnlyList<TValue>>> GetEnumerator()
    {
        foreach (TKey key in _keys)
            yield return new KeyValuePair<TKey, IReadOnlyList<TValue>>(
                key, _values[key]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
