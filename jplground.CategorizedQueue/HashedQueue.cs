namespace jplground.CategorizedQueue;

/// <summary>
/// This is a standard queue qith Add() and GetNext() methods. But it also supports functionality to access the data
/// as if it was a HashSet so that removing items in a O(1) operation.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
/// <remarks>This is not thread-safe. Make sure access is synchronized</remarks>
public class HashedQueue<TKey, TValue> where TKey : notnull
{
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _orderedKeys = new LinkedList<KeyValuePair<TKey, TValue>>();
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _data = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();

    public int Count
    {
        get
        {
            return _data.Count;
        }
    }

#if DEBUG
    public bool ContainsKey(TKey key)
    {
        return _data.ContainsKey(key);
    }
#endif

    /// <summary>
    /// This should only be called by the work processor in order to process work.
    /// </summary>
    public bool TryDequeue([NotNullWhen(true)] out KeyValuePair<TKey, TValue>? next)
    {
        if(_orderedKeys.Count == 0)
        {
            next = default;
            return false;
        }

        // Remove the last one from the linked list as this forms our queue
        LinkedListNode<KeyValuePair<TKey, TValue>> itemToRemove = _orderedKeys.Last!;
        _orderedKeys.Remove(itemToRemove);
        // Then remove the item from the dictionary
        _data.Remove(itemToRemove.Value.Key);
        next = itemToRemove.Value;
        return true;
    }

    /// <summary>
    /// Given the new data provded as the arguments:
    /// - If the key already exists, replaces the value of the existing key with what is provided AND returns the old value.
    /// - If the key doesn't exist, performs the Add() operation.
    /// This should only be called by the worker that adds work to the queue
    /// </summary>
    /// <returns>If the value was replaced, returns the old value</returns>
    public TValue? ReplaceOrAdd(TKey key, TValue value)
    {
        if(key is null)
            throw new ArgumentNullException(nameof(key), $"Cannot pass a null value to {nameof(ReplaceOrAdd)}");
        if(value is null)
            throw new ArgumentNullException(nameof(value), $"Cannot pass a null value to {nameof(ReplaceOrAdd)}");

        if(!_data.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? oldValue))
        {
            Add_Internal(key, value);
            return default;
        }

        // We're replacing the item so the old item will be removed and replaced
        // by a new item at the back of the queue
        _orderedKeys.Remove(oldValue);
        var node = _orderedKeys.AddFirst(new KeyValuePair<TKey, TValue>(key, value));

        // Now the item in the queue has been replaced with the new value.
        // Now we just have to point our key to the new node that was added.
        _data[key] = node;

        return oldValue.Value.Value;
    }

    public bool RemoveIfSame(TKey key, TValue value)
    {
        if(key is null)
            throw new ArgumentNullException(nameof(key), $"Cannot pass a null value to {nameof(RemoveIfSame)}");
        if(value is null)
            throw new ArgumentNullException(nameof(value), $"Cannot pass a null value to {nameof(RemoveIfSame)}");

        if(!_data.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? oldValue))
        {
            // This key doesn't exist here.
            // Nothing to do
            return false;
        }
        
        if(oldValue.Value.Value!.Equals(value))
        {
            // The values are the same. We have to remove this
            _orderedKeys.Remove(oldValue);
            _data.Remove(key);
            return true;
        }
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        if(key is null)
            throw new ArgumentNullException(nameof(key), $"Cannot pass a null value to {nameof(Add)}");
        if(value is null)
            throw new ArgumentNullException(nameof(value), $"Cannot pass a null value to {nameof(Add)}");

        Add_Internal(key, value);
    }

    private void Add_Internal(TKey key, TValue value)
    {
        var node = _orderedKeys.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
        _data.Add(key, node);
    }
}
