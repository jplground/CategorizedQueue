namespace jplground.CategorizedQueue.Tests;

public class CategorizedQueueTests
{
    [Fact]
    public void GivenANewQueue_QueueHasNoWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.HasWorkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GivenANewQueue_NothingShouldBeDequeuable()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.TryDequeue(out var queueItem).Should().BeFalse();
        queueItem.Should().BeNull();
    }

    [Fact]
    public void GivenAQueueWithOneItem_QueueHasWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.Enqueue(Guid.NewGuid(), () => {});
        queue.HasWorkAvailable.Should().BeTrue();
    }

    [Fact]
    public void GivenAQueueWithOneItem_TheItemShouldBeDequeueable()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.Enqueue(Guid.NewGuid(), () => {});
        queue.TryDequeue(out var queueItem).Should().BeTrue();
        queue.HasWorkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GivenAQueueWithOneItem_WhenTheItemHasBeenProcessed_ShouldNotHaveWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.Enqueue(Guid.NewGuid(), () => {});
        queue.TryDequeue(out var queueItem).Should().BeTrue();
        queueItem!.Dispose();
        queue.HasWorkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GivenAQueueWithTwoItemsOfDifferentKeys_WhenTheOneItemHasBeenProcessed_ShouldHaveWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        queue.Enqueue(Guid.NewGuid(), () => {});
        queue.Enqueue(Guid.NewGuid(), () => {});
        queue.TryDequeue(out var queueItem).Should().BeTrue();
        queueItem!.Dispose();
        queue.HasWorkAvailable.Should().BeTrue();
    }

    [Fact]
    public void GivenAQueueWithTwoItemsOfSameKeys_WhenTheOneItemHasBeenProcessed_ShouldHaveWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        var key = Guid.NewGuid();
        queue.Enqueue(key, () => {});
        queue.Enqueue(key, () => {});
        queue.TryDequeue(out var queueItem).Should().BeTrue();
        queue.HasWorkAvailable.Should().BeFalse();
        queueItem!.Dispose();
        queue.HasWorkAvailable.Should().BeTrue();
    }

    [Fact]
    public void GivenAQueueWithTwoItemsOfSameKeys_WhenBothItemsHasBeenProcessed_ShouldNotHaveWork()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        var key = Guid.NewGuid();
        queue.Enqueue(key, () => {});
        queue.Enqueue(key, () => {});
        queue.TryDequeue(out var queueItem).Should().BeTrue();
        queue.HasWorkAvailable.Should().BeFalse();
        queueItem!.Dispose();
        queue.TryDequeue(out queueItem).Should().BeTrue();
        queue.HasWorkAvailable.Should().BeFalse();
        queueItem!.Dispose();
        queue.HasWorkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GivenAQueueWithManyItemsOfSameKeys_WhenAllHaveBeenProcessed_WasProcessedInOrder()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        var key = Guid.NewGuid();
        var results = new List<int>();

        queue.Enqueue(key, () => results.Add(1));
        queue.Enqueue(key, () => results.Add(2));
        queue.Enqueue(key, () => results.Add(3));
        queue.Enqueue(key, () => results.Add(4));
        queue.Enqueue(key, () => results.Add(5));

        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        // There should be nothing left
        queue.TryProcessNextInline().Should().BeFalse();

        results.Should().ContainInOrder(new [] {1, 2, 3, 4, 5});
    }

    [Fact]
    public void GivenAQueueWithManyItemsOfDifferentKeys_WhenAllHaveBeenProcessed_WasProcessedInOrder()
    {
        var queue = new ActionCategorizedQueue<Guid>();
        var results = new List<int>();

        queue.Enqueue(Guid.NewGuid(), () => results.Add(1));
        queue.Enqueue(Guid.NewGuid(), () => results.Add(2));
        queue.Enqueue(Guid.NewGuid(), () => results.Add(3));
        queue.Enqueue(Guid.NewGuid(), () => results.Add(4));
        queue.Enqueue(Guid.NewGuid(), () => results.Add(5));

        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        queue.TryProcessNextInline().Should().BeTrue();
        // There should be nothing left
        queue.TryProcessNextInline().Should().BeFalse();

        results.Should().ContainInOrder(new [] {1, 2, 3, 4, 5});
    }

    [Fact]
    public async void TryWithTasks()
    {
        var queue = new CategorizedQueue<Guid, Task>();
        var taskToEnqueue = new Task<int>(() => 1);
        queue.Enqueue(Guid.NewGuid(), taskToEnqueue);

        queue.TryDequeue(out var removedItem).Should().BeTrue();
        removedItem!.Value.RunSynchronously();
        removedItem.Value.Dispose();
        var result = await taskToEnqueue;
        result.Should().Be(1);
    }
}