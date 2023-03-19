namespace jplground.CategorizedQueue;

public class WorkCoordinator<TCategory, TKey>
    where TCategory : notnull
    where TKey : notnull
{
    // The content of this doesn't change so we don't have to worry about threading.
    // The underlying ActionCategorizedQueue is threadsafe anyway.
    private readonly Dictionary<TCategory, ActionCategorizedQueue<TKey>> _queues;
    private readonly ThreadCacheWithSpareCapacity<TCategory> _workerThreads;
    private Task[] _workerTasks;

    private const int WAIT_FOR_WORK_TIMEOUT_MS = 1;

    public int CategoryCount => _queues.Count;

    public WorkCoordinator(int numSpareThreads, IDictionary<TCategory, int> maxThreadsByKey)
    {
        _workerThreads = new ThreadCacheWithSpareCapacity<TCategory>(numSpareThreads, maxThreadsByKey);
        _queues = maxThreadsByKey.Keys.ToDictionary(cat => cat, cat => new ActionCategorizedQueue<TKey>());

        _workerTasks = new Task[CategoryCount];
    }

    public void Enqueue(TCategory category, TKey key, Action work)
    {
        if(!_queues.ContainsKey(category))
        {
            throw new InvalidOperationException($"Category {category} not recognized. Cannot process work.");
        }

        _queues[category].Enqueue(key, work);
    }

    public async Task StartProcessingLoop(CancellationToken token)
    {
        var queueIndex = 0;
        foreach(var category in _queues.Keys)
        {
            var queue = _queues[category];
            var t = Task.Run(async () =>
            {
                while(!token.IsCancellationRequested)
                {
                    if(!queue.WorkAvailableWaitHandle.WaitOne(WAIT_FOR_WORK_TIMEOUT_MS))
                        continue;
                    // There is work to process
                    if(!queue.TryDequeue(out var queueItem))
                        continue;
                    var workerThreadSubscription = await _workerThreads.WaitNext(category, token);
                    // Start the task (fire-and-forget) on the threadpool
                    _ = Task.Run(() =>
                    {
                        queueItem.Value();
                    }).ContinueWith((ant) => 
                    {
                        // This work item has been processed so we can free it up for the next person.
                        queueItem.Dispose();
                        // We're done with this worker thread. We can return it to the pool.
                        workerThreadSubscription.Dispose();
                    });
                }
            });
            _workerTasks[queueIndex++] = t;
        }

        // And wait until they all complete.
        // Which should be when the cancellation token is done.
        await Task.WhenAll(_workerTasks);
    }
}
