namespace jplground.CategorizedQueue.Tests;

public class TestSynchronizationContext : SynchronizationContext
{
    public Guid Id { get; } = Guid.NewGuid();
}