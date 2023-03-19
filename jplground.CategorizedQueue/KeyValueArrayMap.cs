namespace jplground.CategorizedQueue;

/// <summary>
/// A class to hold a name->value lookup table in two separate arrays so that values can be accessed as a
/// TValue[] while still maintaining functionality to lookup.
/// </summary>
/// <typeparam name="TCategory">The type of Name, usually string or enum</typeparam>
/// <typeparam name="TValue">The type of value</typeparam>
internal class KeyValueArrayMap<TCategory, TValue>
{
    private readonly Dictionary<int, TCategory> _names;
    private readonly TValue[] _data;

    public TValue[] Values => _data;
    public TCategory CategoryFromIndex(int index) => _names[index];

    public KeyValueArrayMap(IList<(TCategory Name, TValue Value)> data)
    {
        if(data.Count == 0)
        {
            throw new ArgumentException($"Cannot initialize an empty {nameof(KeyValueArrayMap<TCategory, TValue>)}");
        }
        _names = new Dictionary<int, TCategory>();
        _data = new TValue[data.Count];

        for(int i = 0; i < data.Count; ++i)
        {
            _names.Add(i, data[i].Name);
            _data[i] = data[i].Value;
        }
    }

    /// <summary>
    /// This will rotate the data in the Values array and shift them one step to the left while still
    /// maintaining the lookup. This can be useful in rare cases, which we'll come to in other code.
    /// </summary>
    public void Rotate()
    {
        // Rotates all the semaphores one step to the left and rolls it around.
        // When we await an array of semaphores we want some varianes in the
        // priority
        var firstData = _data[0];
        var firstCategory = _names[0];

        Array.Copy(_data, 1, _data, 0, _data.Length - 1);
        _data[_data.Length - 1] = firstData;
        for(int i = 0; i < _data.Length - 1; ++i)
        {
            _names[i] = _names[i + 1];
        }
        _names[_data.Length - 1] = firstCategory;
    }
}
