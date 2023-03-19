namespace jplground.CategorizedQueue.Tests;

public class HashedQueueTests
{
    [Fact]
    public void GivenAnEmptyQueue_ThereIsNothingToDequeue()
    {
        var queue = new HashedQueue<object, object>();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void GivenAQueueWithOneItem_ItShouldDequeue()
    {
        var queue = new HashedQueue<Guid, object>();
        var key = Guid.NewGuid();
        var value = new object();
        queue.ReplaceOrAdd(key, value).Should().BeNull();
        queue.TryDequeue(out var next).Should().BeTrue();
        next!.Value.Value.Should().BeSameAs(value);
        next!.Value.Key.Should().Be(key);
    }

    [Fact]
    public void GivenAQueueWithTwoItemsOfTheSameKey_ReplacingOneShouldntChangeTheCount()
    {
        var queue = new HashedQueue<Guid, object>();
        var key = Guid.NewGuid();
        var value1 = new object();
        var value2 = new object();
        queue.ReplaceOrAdd(key, value1).Should().BeNull();
        queue.ReplaceOrAdd(key, value2).Should().Be(value1);
        queue.Count.Should().Be(1);
    }

    [Fact]
    public void GivenAQueueWithTwoItemsWithDifferentKeys_ShouldUpdateTheCount()
    {
        var queue = new HashedQueue<Guid, object>();
        var item1 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        var item2 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        queue.ReplaceOrAdd(item1.Key, item1.Value).Should().BeNull();
        queue.ReplaceOrAdd(item2.Key, item2.Value).Should().BeNull();
        queue.Count.Should().Be(2);
    }

    [Fact]
    public void GivenAQueueWithTwoItemsWithDifferentKeys_DequeuingShouldReturnTheFirstOne()
    {
        var queue = new HashedQueue<Guid, object>();
        var item1 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        var item2 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        queue.ReplaceOrAdd(item1.Key, item1.Value).Should().BeNull();
        queue.ReplaceOrAdd(item2.Key, item2.Value).Should().BeNull();
        queue.TryDequeue(out var next1).Should().BeTrue();

        next1!.Value.Key.Should().Be(item1.Key);
        next1.Value.Value.Should().BeSameAs(item1.Value);

        queue.TryDequeue(out var next2).Should().BeTrue();
        
        next2!.Value.Key.Should().Be(item2.Key);
        next2.Value.Value.Should().BeSameAs(item2.Value);

        queue.Count.Should().Be(0);
    }

    [Fact]
    public void GivenAQueueWithData_ItemIsRemovedIfSame()
    {
        var queue = new HashedQueue<Guid, object>();
        var item1 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        queue.ReplaceOrAdd(item1.Key, item1.Value).Should().BeNull();
        queue.RemoveIfSame(item1.Key, item1.Value).Should().BeTrue();

        queue.Count.Should().Be(0);
    }

    [Fact]
    public void GivenAQueueWithData_ItemIsNotRemovedIfNotSame()
    {
        var queue = new HashedQueue<Guid, object>();
        var item1 = new KeyValuePair<Guid, object>(Guid.NewGuid(), new object());
        queue.ReplaceOrAdd(item1.Key, new object()).Should().BeNull();
        queue.RemoveIfSame(item1.Key, item1.Value).Should().BeFalse();

        queue.Count.Should().Be(1);
    }

    [Fact]
    public void GivenLotsOfItemsAddedToTheQueue_TheDequeueIsDoneInOrderOfAdding()
    {
        var keys = Enumerable.Range(0, 1000).Select(i => Guid.NewGuid()).ToArray();
        var queue = new HashedQueue<Guid, object>();
        foreach(var key in keys)
        {
            queue.Add(key, new object());
        }
        var dequeued = new List<Guid>();
        while(queue.TryDequeue(out var next))
        {
            dequeued.Add(next.Value.Key);
        }
        dequeued.Should().ContainInOrder(keys);
        dequeued.Should().HaveCount(keys.Length);
    }
}