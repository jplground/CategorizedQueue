namespace jplground.CategorizedQueue;

public class ThreadCacheWithSpareCapacity<TCategory> : IDisposable where TCategory : notnull
{
    private const int KEY_POOL_INDEX = 0;
    private const int SPARE_POOL_INDEX = 1;
    private const int TIMEOUT_SEMAPHORE_WAIT_MS = 1;
    // We're storing the semaphores in an array so that we can use it in the WaitHandle.WaitAny call
    private readonly Dictionary<TCategory, Semaphore[]> _threadsByKey = new Dictionary<TCategory, Semaphore[]>();

    public TCategory[] AllCategories { get; }

    public ThreadCacheWithSpareCapacity(int numSpareThreads, IDictionary<TCategory, int> maxThreadsByKey)
    {
        if(maxThreadsByKey.Count == 0)
        {
            throw new ArgumentException($"Cannot create a {nameof(ThreadCacheWithSpareCapacity<TCategory>)} with zero key-threads. It just doesn't work that way.");
        }

        var totalThreadsByKey = maxThreadsByKey.Values.Sum();

        var sparePoolThreads = new Semaphore(numSpareThreads, numSpareThreads);
        // We have to have at least one entry in the semaphore otherwise it will throw.
        // If the setup said zero, then we can mimick that by just having all of them signalled so that no further work is available.
        _threadsByKey = maxThreadsByKey.ToDictionary(kv => kv.Key, kv => new [] {new Semaphore(kv.Value, Math.Max(1, kv.Value)), sparePoolThreads});

        AllCategories = maxThreadsByKey.Keys.ToArray();
    }

    public void Dispose()
    {
        // We have to dispose of the Semaphores

        // We only have one sparePoolThreads so we just have to dispose one of them.
        _threadsByKey.First().Value[1].Dispose();
        foreach(var v in _threadsByKey.Values)
        {
            v[0].Dispose();
        }
    }

    public bool HasCapacityFor(TCategory key)
    {
        if(!_threadsByKey.TryGetValue(key, out var semaphoresByKey))
        {
            throw new Exception($"Did not have configuration for key {key}. Can't process it.");
        }
        var indexOfSemaphore = WaitHandle.WaitAny(semaphoresByKey, millisecondsTimeout: 0);

        return indexOfSemaphore != WaitHandle.WaitTimeout;
    }

    public async Task<IDisposable> WaitNext(TCategory key, CancellationToken token)
    {
        // In order for us to have capacity, we have to first have capacity in the spare pool
        // If we don't have this, then we're maxed out regardless.
        if(!_threadsByKey.TryGetValue(key, out var keyThread))
        {
            throw new Exception($"Did not have configuration for key {key}. Can't process it.");
        }

        var signaledWaitHandleIndex = await WaitHandleExtensions.WaitAnyAsync(keyThread, token).ConfigureAwait(false);

        return new WorkerItem(this, key, signaledWaitHandleIndex == KEY_POOL_INDEX);
    }

    private void ReleaseWorkerItem(WorkerItem item)
    {
        if(item.IsFromKeyPool)
        {
            _threadsByKey[item.Key][KEY_POOL_INDEX].Release();
        }
        else
        {
            _threadsByKey[item.Key][SPARE_POOL_INDEX].Release();
        }
    }

    private record WorkerItem : IDisposable
    {
        private readonly ThreadCacheWithSpareCapacity<TCategory> _parent;
        public bool IsFromKeyPool { get; }
        public TCategory Key { get; }

        public WorkerItem(ThreadCacheWithSpareCapacity<TCategory> parent, TCategory key, bool isFromKeyPool)
        {
            _parent = parent;
            Key = key;
            IsFromKeyPool = isFromKeyPool;
        }

        public void Dispose()
        {
            _parent.ReleaseWorkerItem(this);
        }
    }
}