namespace jplground.CategorizedQueue.Tests;

public class WaitHandleExtensionsTests
{
    [Fact]
    public void GivenAVanillaSemaphore_ItShouldWorkAsExpected()
    {
        using var semaphore = new Semaphore(1,1);
        semaphore.WaitOne(0).Should().BeTrue();
        semaphore.WaitOne(0).Should().BeFalse();
    }

    [Fact]
    public async Task GivenASemaphoreWrappedAsATask_ItShouldWorkAsExpectedAsync()
    {
        using var semaphore = new Semaphore(1,1);
        await semaphore.ToTask();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        // We verify that the token has been cancelled. I.e. the task waited until the cancellation expired.
        await semaphore.ToTask().Invoking(async t => await t.WaitAsync(cts.Token)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GivenASemaphoreWrappedAsATask_ItShouldWorkAsExpectedSync()
    {
        using var semaphore = new Semaphore(1,1);
        await semaphore.ToTask();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        // We verify that the token has been cancelled. I.e. the task waited until the cancellation expired.
        semaphore.ToTask().Invoking(t => t.Wait(cts.Token)).Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GivenTwoSemaphores_WaitAnyAsyncShouldWork(int indexToSignal)
    {
        var semaphores = new [] {new Semaphore(1, 1), new Semaphore(1,1)};
        semaphores[indexToSignal].WaitOne();
        var signalled = await WaitHandleExtensions.WaitAnyAsync(semaphores, CancellationToken.None);
        signalled.Should().Be(indexToSignal == 0 ? 1 : 0);
    }
}