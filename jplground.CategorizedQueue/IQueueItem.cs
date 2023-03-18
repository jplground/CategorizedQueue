namespace jplground.CategorizedQueue;

public interface IQueueItem<TKey, TValue> : IDisposable
{
    TKey Key { get; }
    TValue Value { get; }
}
