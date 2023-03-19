namespace jplground.CategorizedQueue.Tests;

public class ActionCategorizedQueue<T> : CategorizedQueue<T, Action> where T : notnull
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