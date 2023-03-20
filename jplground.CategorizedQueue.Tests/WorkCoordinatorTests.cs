namespace jplground.CategorizedQueue.Tests;

public class WorkCoordinatorTests
{
    private enum WorkItemCategory
    {
        None = 0,
        Kafka,
        ApsNet,
        gRpc,
        Orleans
    }

    private Dictionary<WorkItemCategory, int> OnlySpareCapacitySetup => new Dictionary<WorkItemCategory, int>()
    {
        {WorkItemCategory.None, 1},
        {WorkItemCategory.Kafka, 0}
    };

    [Fact(Timeout = 1_000)]
//    [Fact]
    public async Task GivenAQueueWithWork_AndThenTheProcessingLoopStarts_WorkIsProcessed()
    {
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        ManualResetEventSlim evt = new ManualResetEventSlim();
        var enqueuedTask = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => {evt.Set();});
        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);
        evt.Wait();
        await enqueuedTask;

        // And clean up
        cts.Cancel();
        await t;
    }

    [Fact(Timeout = 1_000)]
//    [Fact]
    public async Task GivenWorkWithAReturnValue_TheReturnValueIsAvailable()
    {
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        var enqueuedTask = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => 42);
        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);
        var result = await enqueuedTask;
        result.Should().Be(42);

        // And clean up
        cts.Cancel();
        await t;
    }

    [Fact(Timeout = 1_000)]
//    [Fact]
    public async Task GivenAQueueWithNo_AndTheProcessingLoopStarts_ThenWorkIsEnqueued_WorkIsProcessed()
    {
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        ManualResetEventSlim evt = new ManualResetEventSlim();
        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);

        await workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => {evt.Set();});
        evt.Wait();

        // And clean up
        cts.Cancel();
        await t;
    }

//    [Fact]
    [Fact(Timeout = 10_000)]
    public async Task GivenLotsOfTasksOfTheSameKey_TheyShouldProcessInOrder()
    {
        var key = Guid.NewGuid();
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        var results = new List<int>();
        var enqueued = new List<Task>();

        var numTasks = 10_000;

        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);

        for(int i = 0; i < numTasks; ++i)
        {
            int id = i;
            enqueued.Add(workCoordinator.Enqueue(WorkItemCategory.Kafka, key, () => {results.Add(id);}));
        }

        foreach(var q in enqueued)
        {
            await q;
        }

        results.Count.Should().Be(numTasks);
        for(int i = 0; i < numTasks - 1; ++i)
        {
            results[i].Should().Be(results[i + 1] - 1);
        }

        cts.Cancel();
        await t;
    }

    [Fact(Timeout=5000)]
    public async Task GivenAPoolWith2Threads_Only2ThreadsCanRunConcurrently()
    {
        Dictionary<WorkItemCategory, int> oneWorkerThread = new Dictionary<WorkItemCategory, int>()
        {
            {WorkItemCategory.Kafka, 1}
        };

        var key = Guid.NewGuid();
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, oneWorkerThread);

        var cts = new CancellationTokenSource();
        var loop = workCoordinator.StartProcessingLoop(cts.Token);

        var blocker = new ManualResetEventSlim();
        var t1Started = new ManualResetEventSlim();
        var t2Started = new ManualResetEventSlim();
        var t3Started = new ManualResetEventSlim();
        var t1 = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () =>
        {
            t1Started.Set();
            blocker.Wait();
        });
        var t2 = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () =>
        {
            t2Started.Set();
            blocker.Wait();
        });
        // This one shouldn't start because 
        var t3 = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () =>
        {
            t3Started.Set();
            blocker.Wait();
        });

        t1Started.Wait();
        t2Started.Wait();
        await Task.Delay(1000);
        // This shouldn't have started
        t3Started.Wait(0).Should().BeFalse();
        // Now let them all go
        blocker.Set();
        await Task.Delay(1000);
        t3Started.Wait(0).Should().BeTrue();

        await t1;
        await t2;
        await t3;

        cts.Cancel();
        await loop;

    }
}