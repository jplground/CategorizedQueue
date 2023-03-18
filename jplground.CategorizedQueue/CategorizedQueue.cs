namespace jplground.CategorizedQueue;

public interface IQueueItem<TKey, TValue> : IDisposable
{
    TKey Key { get; }
    TValue Value { get; }
}

public class AssertionException : Exception
{
    public AssertionException(string message)
        : base(message)
    {
        
    }
}

public abstract class CategorizedQueue<TKey, TValue> : IDisposable where TKey : notnull
{
    public bool HasWorkAvailable => _hasPendingWork.IsSet;
    private ManualResetEventSlim _hasPendingWork = new ManualResetEventSlim();

    private readonly HashedQueue<TKey, QueueItem> _workThatBlocks = new HashedQueue<TKey, QueueItem>();
    private readonly HashedQueue<TKey, QueueItem> _workThatIsReadyToProcess = new HashedQueue<TKey, QueueItem>();

    private readonly object _locker = new object();

    /// <summary>
    /// Enqueues a new item on the queue
    /// </summary>
    /// <param name="key">The key of the item</param>
    /// <param name="action">The action to perform</param>
    /// <returns>An awaiting task that completes when the action has been executed</returns>
    public void Enqueue(TKey key, TValue value)
    {
        lock(_locker)
        {
            var queueItem = new QueueItem(this, new QueueNode<TKey, TValue>(key, value));
            QueueItem? replacedNode = _workThatBlocks.ReplaceOrAdd(key, queueItem);
            if(replacedNode is null)
            {
                // We didn't replace anything. So this one wasn't blocked by anything
                // So we can add it to the processing queue immediately.
                _workThatIsReadyToProcess.Add(key, queueItem);
                _hasPendingWork.Set();
            }
            else
            {
                replacedNode.QueueNode.NodeBlockedByThis = queueItem.QueueNode;
            }
        }
    }

    /// <summary>
    /// This is protected internal because the way this works is that the returned queueItem HAS TO
    /// be Dispose()'d when it has been processed. Therefore the responsibility for doing so should be
    /// in a library, and not in business logic code. This pattern forces inheritance and therefore
    /// (hopefully) more re-usable code.
    /// </summary>
    /// <param name="queueItem"></param>
    /// <returns></returns>
    protected internal bool TryDequeue([NotNullWhen(true)] out IQueueItem<TKey, TValue>? queueItem)
    {
        lock(_locker)
        {
            if(!_workThatIsReadyToProcess.TryDequeue(out KeyValuePair<TKey, QueueItem>? next))
            {
                // There was nothing to process.
                // Don't do anything
                queueItem = null;
                return false;
            }

            queueItem = next.Value.Value;
            if(_workThatIsReadyToProcess.Count == 0)
            {
                // We dequeued the last one. No more work to do.
                _hasPendingWork.Reset();
            }
            return true;
        }
    }

    private void WorkHasCompleted(QueueItem queueItem)
    {
        lock(_locker)
        {
#if DEBUG
            if(_workThatIsReadyToProcess.ContainsKey(queueItem.QueueNode.Key))
            {
                throw new AssertionException($"A unit of work has completed. But the unit exists in the {nameof(_workThatIsReadyToProcess)} collection.");
            }
#endif

            // Something that was dequeud from _workThatIsReadyToProcess has completed.

            var blockingNode = queueItem.QueueNode.NodeBlockedByThis;
            if(blockingNode is null)
            {
                // This completed work isn't blocking anything
                // So we have to just remove everything about it.
                _workThatBlocks.RemoveIfSame(queueItem.QueueNode.Key, queueItem);
            }
            else
            {
                // This unit was blocking something else. Move that thing into the work ready to process section
                _workThatIsReadyToProcess.Add(queueItem.QueueNode.Key, new QueueItem(this, blockingNode));
                _hasPendingWork.Set();
            }
        }
    }

    public void Dispose()
    {
        _hasPendingWork.Dispose();
    }

    private class QueueItem : IQueueItem<TKey, TValue>
    {
        public QueueNode<TKey, TValue> QueueNode { get; }

        public TKey Key => QueueNode.Key;
        public TValue Value => QueueNode.Value;

        private readonly CategorizedQueue<TKey, TValue> _parent;
        private bool _disposed = false;

        public QueueItem(CategorizedQueue<TKey, TValue> parent, QueueNode<TKey, TValue> queueNode)
        {
            _parent = parent;
            QueueNode = queueNode;
        }

        public void Dispose()
        {
            if(_disposed)
            {
                throw new AssertionException($"Disposed called a second time on {nameof(QueueItem)} with key {Key}");
            }

            // If this throws, we're in a real mess.
            // Shouldn't call it disposed if that happens, but also don't want to not dispose it.
            _parent.WorkHasCompleted(this);
            _disposed = true;
        }

        // Arguably there should be a destructor here to verify that it won't be called.
        // But that will just slow things down. Leaving it without one and will do suitable testing.
    }
}
