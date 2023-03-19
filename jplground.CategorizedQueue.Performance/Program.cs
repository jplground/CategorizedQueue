namespace jplground.CategorizedQueue.Performance;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<Benchmarks>();
    }
}
