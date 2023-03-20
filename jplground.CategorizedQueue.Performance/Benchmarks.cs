using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis;

namespace jplground.CategorizedQueue.Performance;

public class Benchmarks
{
    // 27: 0.27sec
    // 30: 2sec
    // 32: 9sec
    // 33: 17sec
    // 35: 70sec
    private const int TOWER_SIZE = 27;
    private const int NUM_WORKER_THREADS = 20;

    // [Params(1)]
    [Params(1, 2, 3, 5, 10, 20, 60, 100, 150, 200)]
    public int DegreesOfParallelism { get; set; }

    public Benchmarks()
    {
        // Prime the test to have threads to do the work.
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(60, completionPortThreads);
    }

// Some conclusions on this:
// - Windows is really good at context switching
// - Choking the number of parallel tasks to the number of cores has the same throughput as not choking it at all.
// - Choking it opens the door to allowing tasks to re-prioritise tasks before they start executing.
/*
|                         Method | DegreesOfParallelism |       Mean | Error |    StdDev |
|------------------------------- |--------------------- |-----------:|------:|----------:|
|             SolveTowersOfHanoi |                    1 |   270.8 ms |    NA |   0.89 ms |
| SolveTowersOfHanoi_Categorized |                    1 |   266.1 ms |    NA |   1.13 ms |
|             SolveTowersOfHanoi |                    2 |   295.7 ms |    NA |   8.20 ms |
| SolveTowersOfHanoi_Categorized |                    2 |   300.0 ms |    NA |   0.38 ms |
|             SolveTowersOfHanoi |                    3 |   305.0 ms |    NA |  28.88 ms |
| SolveTowersOfHanoi_Categorized |                    3 |   297.1 ms |    NA |   5.80 ms |
|             SolveTowersOfHanoi |                    5 |   342.3 ms |    NA |  11.49 ms |
| SolveTowersOfHanoi_Categorized |                    5 |   360.9 ms |    NA |   8.37 ms |
|             SolveTowersOfHanoi |                   10 |   517.9 ms |    NA |  16.81 ms |
| SolveTowersOfHanoi_Categorized |                   10 |   532.5 ms |    NA |  32.55 ms |
|             SolveTowersOfHanoi |                   20 |   749.8 ms |    NA |  16.78 ms |
| SolveTowersOfHanoi_Categorized |                   20 |   792.1 ms |    NA |  22.60 ms |
|             SolveTowersOfHanoi |                   60 | 2,374.5 ms |    NA |  48.98 ms |
| SolveTowersOfHanoi_Categorized |                   60 | 2,469.4 ms |    NA |  82.57 ms |
|             SolveTowersOfHanoi |                  100 | 3,845.0 ms |    NA |  48.67 ms |
| SolveTowersOfHanoi_Categorized |                  100 | 3,853.9 ms |    NA | 146.63 ms |
|             SolveTowersOfHanoi |                  150 | 5,639.6 ms |    NA |  28.77 ms |
| SolveTowersOfHanoi_Categorized |                  150 | 5,810.8 ms |    NA |  11.41 ms |
|             SolveTowersOfHanoi |                  200 | 7,512.1 ms |    NA |   0.98 ms |
| SolveTowersOfHanoi_Categorized |                  200 | 7,782.8 ms |    NA |  97.93 ms |
*/
    [Benchmark]
    [IterationCount(2)]
    [WarmupCount(2)]
    public async Task SolveTowersOfHanoi()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < DegreesOfParallelism; ++i)
        {
            var task = Task.Run(() => TowersOfHanoi.SolveTowers(TOWER_SIZE));
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private enum WorkItemCategory
    {
        None = 0,
        Kafka = 1
    }

    [Benchmark]
    [IterationCount(2)]
    [WarmupCount(2)]
    public async Task SolveTowersOfHanoi_Categorized()
    {
        // Running on 5 + 1 thread
        Dictionary<WorkItemCategory, int> oneWorkerThread = new Dictionary<WorkItemCategory, int>()
        {
            {WorkItemCategory.Kafka, NUM_WORKER_THREADS - 1}        // -1 because we have one in the spare pool
        };

        var workCoordinator = new WorkCoordinator<WorkItemCategory, Guid>(1, oneWorkerThread);

        var cts = new CancellationTokenSource();
        var loop = workCoordinator.StartProcessingLoop(cts.Token);
        
        var tasks = new List<Task>();
        for (int i = 0; i < DegreesOfParallelism; ++i)
        {
            var task = workCoordinator.Enqueue(WorkItemCategory.Kafka, Guid.NewGuid(), () => TowersOfHanoi.SolveTowers(TOWER_SIZE));
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        cts.Cancel();
        await loop;
    }
}
