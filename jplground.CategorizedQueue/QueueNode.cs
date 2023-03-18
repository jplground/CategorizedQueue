namespace jplground.CategorizedQueue;

internal record QueueNode<Tkey, TValue>(Tkey Key, TValue Value)
{
    public QueueNode<Tkey, TValue>? NodeBlockedByThis { get; set; }
}
