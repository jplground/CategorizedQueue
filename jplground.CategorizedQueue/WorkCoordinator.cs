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

    private class WorkCoordinatorSynchronizationContext : SynchronizationContext
    {
        private TCategory _category;
        private TKey _key;
        private WorkCoordinator<TCategory, TKey> _workCoordinator;

        public WorkCoordinatorSynchronizationContext(WorkCoordinator<TCategory, TKey> workCoordinator, TCategory category, TKey key)
        {
            _workCoordinator = workCoordinator;
            _category = category;
            _key = key;
        }

        public override SynchronizationContext CreateCopy()
        {
            return new WorkCoordinatorSynchronizationContext(_workCoordinator, _category, _key);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            _workCoordinator.Enqueue(_category, _key, () => d(state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            _workCoordinator.Enqueue(_category, _key, () => d(state)).Wait();
        }

        public override void OperationCompleted()
        {
            base.OperationCompleted();
        }

        public override void OperationStarted()
        {
            base.OperationStarted();
        }
    }

    public WorkCoordinator(int numSpareThreads, IDictionary<TCategory, int> maxThreadsByKey)
    {
        _workerThreads = new ThreadCacheWithSpareCapacity<TCategory>(numSpareThreads, maxThreadsByKey);
        _queues = maxThreadsByKey.Keys.ToDictionary(cat => cat, cat => new CategorizedQueue<TKey, Task>());

        _workerTasks = new Task[CategoryCount];
    }

    public SynchronizationContext GetSynchronizationContextFor(TCategory category, TKey key)
    {
        return new WorkCoordinatorSynchronizationContext(this, category, key);
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
                    await queue.WorkAvailableWaitHandle.ToTask().WaitAsync(token).ConfigureAwait(false);

                    // There is work to process
                    if(!queue.TryDequeue(out var queueItem))
                        continue;
                    var workerThreadSubscription = await _workerThreads.WaitNext(category, token).ConfigureAwait(false);
                    // We got this far and we're ready to run the task.
                    // We HAVE TO run this on the threadpool with the default
                    // SynchronizationContext, otherwise it'll come back into the whole
                    // loop again... but... what we do is set the synchronization context after it starts
                    // so that continuations and awaits resume by going through the whole framework
                    var sc = queueItem.SynchronizationContext ?? new SynchronizationContext();
                    // Start the task (fire-and-forget) on the 
                    _ = Task.Run(() =>  
                    {
                        SynchronizationContext.SetSynchronizationContext(sc);
                        try
                        {
                            // BUG: This seems to post back to the synchronization context
                            // We don't want this. We want this to run normally inline.
                            queueItem.Value.RunSynchronously();
                            // Note: This will mark the running Task as completed,
                            // with a result before the threads and queues
                            // have noticed the completion.
                            // This should be fine as we don't really care.
                        }
                        catch
                        {
                            // TODO: Logging
                        }

                        // That was run on the synchronization context of the caller.
                        // We're now done so we can switch back to the default synchronization context
                        // to clean up.
                        _ = Task.Run(() =>
                        {
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
                    });
                }
            });
            _workerTasks[queueIndex++] = t;
        }

        // And wait until they all complete.
        // Which should be when the cancellation token is done.
        try
        {
            await Task.WhenAll(_workerTasks).ConfigureAwait(false);
        }
        catch(TaskCanceledException)
        {
            // This is expected. We should just log and exist cleanly here.
            // TODO: Logging
        }
    }
}
