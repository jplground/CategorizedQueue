namespace jplground.CategorizedQueue.Tests;

public class SynchronizationContextTests
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

    [Fact]
    public async Task SynchronizationContextTest()
    {
        var cts = new CancellationTokenSource();

        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, OnlySpareCapacitySetup);
        var t = workCoordinator.StartProcessingLoop(cts.Token);

        var sc = workCoordinator.GetSynchronizationContextFor(WorkItemCategory.Kafka, Guid.NewGuid());
        SynchronizationContext.SetSynchronizationContext(sc);

        SynchronizationContext.Current.Should().BeSameAs(sc);

        await Task.Delay(1);

        SynchronizationContext.Current.Should().BeSameAs(sc);
        TaskScheduler.FromCurrentSynchronizationContext();
    }
}