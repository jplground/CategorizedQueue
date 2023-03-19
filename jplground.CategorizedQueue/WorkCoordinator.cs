namespace jplground.CategorizedQueue;

public class WorkCoordinator<TCategory, TKey>
    where TCategory : notnull
    where TKey : notnull
{
    // The content of this doesn't change so we don't have to worry about threading.
    // The underlying ActionCategorizedQueue is threadsafe anyway.
    private readonly Dictionary<TCategory, CategorizedQueue<TKey, Task>> _queues;
    private readonly ThreadCacheWithSpareCapacity<TCategory> _workerThreads;
    private Task[] _workerTasks;

    public int CategoryCount => _queues.Count;

    public WorkCoordinator(int numSpareThreads, IDictionary<TCategory, int> maxThreadsByKey)
    {
        _workerThreads = new ThreadCacheWithSpareCapacity<TCategory>(numSpareThreads, maxThreadsByKey);
        _queues = maxThreadsByKey.Keys.ToDictionary(cat => cat, cat => new CategorizedQueue<TKey, Task>());

        _workerTasks = new Task[CategoryCount];
    }

    public Task<T> Enqueue<T>(TCategory category, TKey key, Func<T> work)
    {
        if(!_queues.TryGetValue(category, out var queue))
        {
            throw new InvalidOperationException($"Category {category} not recognized. Cannot process work.");
        }

        var enqueuedTask = new Task<T>(work);
        queue.Enqueue(key, enqueuedTask);
        return enqueuedTask;
    }

    public Task Enqueue(TCategory category, TKey key, Action work)
    {
        if(!_queues.TryGetValue(category, out var queue))
        {
            throw new InvalidOperationException($"Category {category} not recognized. Cannot process work.");
        }

        var enqueuedTask = new Task(work);
        queue.Enqueue(key, enqueuedTask);
        return enqueuedTask;
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
                    // Wait for work
                    await queue.WorkAvailableWaitHandle.ToTask().WaitAsync(token);

                    // There is work to process
                    if(!queue.TryDequeue(out var queueItem))
                        continue;
                    var workerThreadSubscription = await _workerThreads.WaitNext(category, token);
                    // Start the task (fire-and-forget) on the threadpool
                    _ = Task.Run(() =>
                    {
                        // TODO: This should be run on the enqueuers synchronization context
                        queueItem.Value.RunSynchronously();
                    }).ContinueWith((ant) => 
                    {
                        if(ant.Status == TaskStatus.Faulted)
                        {
                            // TODO: Logging
                        }

                        try
                        {
                            // This work item has been processed so we can free it up for the next person.
                            queueItem.Dispose();
                        }
                        catch 
                        {
                            // TODO: Logging
                        }
                        try
                        {
                            // We're done with this worker thread. We can return it to the pool.
                            workerThreadSubscription.Dispose();
                        }
                        catch 
                        {
                            // TODO: Logging
                        }
                    });
                }
            });
            _workerTasks[queueIndex++] = t;
        }

        // And wait until they all complete.
        // Which should be when the cancellation token is done.
        try
        {
            await Task.WhenAll(_workerTasks);
        }
        catch(TaskCanceledException)
        {
            // This is expected. We should just log and exist cleanly here.
            // TODO: Logging
        }
    }
}
