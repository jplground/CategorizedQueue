using System.Runtime.CompilerServices;

namespace jplground.CategorizedQueue;

public static class WaitHandleExtensions
{
 // Commenting these out for now as they don't work the way we want them to
    public static Task ToTask(this WaitHandle waitHandle)
    {
        if (waitHandle == null)
            throw new ArgumentNullException(nameof(waitHandle));

        var tcs = new TaskCompletionSource<bool>();
        var rwh = ThreadPool.RegisterWaitForSingleObject(
            waitObject: waitHandle,
            callBack: delegate { tcs.TrySetResult(true); },
            state: null,
            millisecondsTimeOutInterval: -1,
            executeOnlyOnce: true);
            
        var t = tcs.Task;
        t.ContinueWith((ant) => rwh.Unregister(null));
        return t;
    }

/*
    public static int WaitAny(WaitHandle[] waitHandles, CancellationToken token)
    {
        var taskArray = new Task[waitHandles.Length];
        for(int i = 0; i < waitHandles.Length; ++i)
        {
            taskArray[i] = waitHandles[i].ToTask();
        }
        return Task.WaitAny(taskArray, token);
    }
*/

    public static Task<int?> WaitAnyAsync(WaitHandle[] waitHandles, CancellationToken token)
    {
        const int TIMEOUT_SEMAPHORE_WAIT_MS = 10;
            
        return Task<int?>.Run(() => 
        {
            while(!token.IsCancellationRequested)
            {
                var indexOfSemaphore = WaitHandle.WaitAny(waitHandles, TIMEOUT_SEMAPHORE_WAIT_MS);

                if(indexOfSemaphore != WaitHandle.WaitTimeout)
                {
                    return indexOfSemaphore;
                }
            }
            return (int?)null;
        }, token);       // Need this to run on the threadpool to work.
    }
}
