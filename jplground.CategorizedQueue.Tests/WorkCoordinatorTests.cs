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
        workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => {evt.Set();});
        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);
        evt.Wait();

        // And clean up
        cts.Cancel();
        await t;
    }

    [Fact]
    public async Task GivenAQueueWithNo_AndTheProcessingLoopStarts_ThenWorkIsEnqueued_WorkIsProcessed()
    {
        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        ManualResetEventSlim evt = new ManualResetEventSlim();
        var cts = new CancellationTokenSource();
        var t = workCoordinator.StartProcessingLoop(cts.Token);

        workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => {evt.Set();});
        evt.Wait();

        // And clean up
        cts.Cancel();
        await t;
    }
}