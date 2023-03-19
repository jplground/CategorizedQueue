using System.Runtime.CompilerServices;

namespace jplground.CategorizedQueue;

public static class WaitHandleExtensions
{
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

    public static int WaitAny(WaitHandle[] waitHandles, CancellationToken token)
    {
        var taskArray = new Task[waitHandles.Length];
        for(int i = 0; i < waitHandles.Length; ++i)
        {
            taskArray[i] = waitHandles[i].ToTask();
        }
        return Task.WaitAny(taskArray, token);
    }

    public static Task<int> WaitAnyAsync(WaitHandle[] waitHandles, CancellationToken token)
    {
        // Task.Run will always use the threadpool so we don't have to worry about the SynchronizationContext here.
        return Task.Run(() => {
            return WaitAny(waitHandles, token);
        }, token);
    }
}