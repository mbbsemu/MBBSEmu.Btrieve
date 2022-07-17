using System.Collections.Generic;

namespace MBBSEmu.Btrieve.Cache;

public interface ILRUCache<TKey, TValue>
{
    /// <summary>
    ///   The maximum number of items this collection will hold.
    /// </summary>
    /// <value></value>
    int MaxSize { get; init; }

    /// <summary>
    ///   The number of items currently inside this cache.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///   The number of items tracked inside _recentlyUsedList. You probably want to use Count instead.
    ///
    ///   <para/>This is mostly a test-only property, don't rely on this value in real code.
    /// </summary>
    /// <value></value>
    int ListCount { get; }

    /// <summary>
    ///   The most recently used key.
    /// </summary>
    TKey MostRecentlyUsed { get; }

    bool IsReadOnly { get; }
    ICollection<TKey> Keys { get; }
    ICollection<TValue> Values { get; }

    TValue this[TKey key] { get; set; }
    void Add(KeyValuePair<TKey, TValue> item);
    void Add(TKey key, TValue value);
    void Clear();
    bool Contains(KeyValuePair<TKey, TValue> item);
    bool ContainsKey(TKey key);
    void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex);
    bool Remove(KeyValuePair<TKey, TValue> item);
    bool Remove(TKey key);
    bool TryGetValue(TKey key, out TValue value);
    IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
}