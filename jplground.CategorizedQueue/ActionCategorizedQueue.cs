namespace jplground.CategorizedQueue;

public class ActionCategorizedQueue<TKey> : CategorizedQueue<TKey, Action> where TKey : notnull
{
    public bool TryProcessNextInline()
    {
        if(!this.TryDequeue(out var queueItem))
        {
            // Nothing to process
            return false;
        }

        try
        {
            queueItem.Value();
            return true;
        }
        finally
        {
            queueItem.Dispose();
        }
    }
}
