namespace jplground.CategorizedQueue;

public class WorkCoordinator<TCategory, TKey>
    where TCategory : notnull
    where TKey : notnull
{
    private interface IUnitOfWork
    {
        void Execute();
    }

    // The content of this doesn't change so we don't have to worry about threading.
    // The underlying ActionCategorizedQueue is thread-safe anyway.
    private readonly Dictionary<TCategory, CategorizedQueue<TKey, IUnitOfWork>> _queues;
    private readonly ThreadCacheWithSpareCapacity<TCategory> _workerThreads;

    private int CategoryCount => _queues.Count;

    public WorkCoordinator(int numSpareThreads, IDictionary<TCategory, int> maxThreadsByKey)
    {
        _workerThreads = new ThreadCacheWithSpareCapacity<TCategory>(numSpareThreads, maxThreadsByKey);
        _queues = maxThreadsByKey.Keys.ToDictionary(cat => cat, cat => new CategorizedQueue<TKey, IUnitOfWork>());
    }

    public Task<T> Enqueue<T>(TCategory category, TKey key, Func<T> work)
    {
        if(!_queues.TryGetValue(category, out var queue))
        {
            throw new InvalidOperationException($"Category {category} not recognized. Cannot process work.");
        }

        var tcs = new TaskCompletionSource<T>();
        queue.Enqueue(key, new FuncUnitOfWork<T>(work, tcs));
        return tcs.Task;
    }

    public Task Enqueue(TCategory category, TKey key, Action work)
    {
        if(!_queues.TryGetValue(category, out var queue))
        {
            throw new InvalidOperationException($"Category {category} not recognized. Cannot process work.");
        }

        var tcs = new TaskCompletionSource();
        queue.Enqueue(key, new ActionUnitOfWork(work, tcs));
        return tcs.Task;
    }

    public async Task StartProcessingLoop(CancellationToken token)
    {
        var workerTasks = new Task[CategoryCount];

        var queueIndex = 0;
        foreach(var kv in _queues)
        {
            var queue = kv.Value;
            var t = Task.Run(async () =>
            {
                while(!token.IsCancellationRequested)
                {
                    // Wait for work
                    await queue.WorkAvailableWaitHandle.ToTask().WaitAsync(token).ConfigureAwait(false);

                    // There is work to process
                    if(!queue.TryDequeue(out var queueItem))
                        continue;

                    // Wait for a thread to do this work
                    var workerThreadSubscription = await _workerThreads.WaitNext(kv.Key, token);
                    // Start the task (fire-and-forget) on the threadpool
                    _ = Task.Run(() =>
                    {
                        // TODO: This should be run on the enqueuers synchronization context
                        queueItem.Value.Execute();
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
            workerTasks[queueIndex++] = t;
        }

        // And wait until they all complete.
        // Which should be when the cancellation token is done.
        try
        {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        catch(TaskCanceledException)
        {
            // This is expected. We should just log and exist cleanly here.
            // TODO: Logging
        }
    }

    private class ActionUnitOfWork : IUnitOfWork
    {
        private readonly TaskCompletionSource _tcs;
        private readonly Action _action;

        public ActionUnitOfWork(Action action, TaskCompletionSource tcs)
        {
            _tcs = tcs;
            _action = action;
        }

        public void Execute()
        {
            try
            {
                _action();
                _tcs.SetResult();
            }
            catch (Exception exc)
            {
                _tcs.SetException(exc);
            }
        }
    }

    private class FuncUnitOfWork<T> : IUnitOfWork
    {
        private readonly TaskCompletionSource<T> _tcs;
        private readonly Func<T> _func;
        
        public FuncUnitOfWork(Func<T> func, TaskCompletionSource<T> tcs)
        {
            _tcs = tcs;
            _func = func;
        }

        public void Execute()
        {
            try
            {
                var result = _func();
                _tcs.SetResult(result);
            }
            catch (Exception exc)
            {
                _tcs.SetException(exc);
            }
        }
    }
}
