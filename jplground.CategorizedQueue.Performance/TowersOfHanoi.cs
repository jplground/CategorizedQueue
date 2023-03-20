namespace jplground.CategorizedQueue.Performance;

/// <summary>
/// 
/// </summary>
/// <remarks>"Borrowed" from https://www.csharpstar.com/towers-of-hanoi-in-csharp/</remarks>
public class TowersOfHanoi
{
    public static void SolveTowers(int totalDisks)
    {
        SolveTowers(totalDisks, 'A', 'C', 'B');
    }

    private static void SolveTowers(int n, char startPeg, char endPeg, char tempPeg)
    {
        if (n > 0)
        {
            SolveTowers(n - 1, startPeg, tempPeg, endPeg);
            // "Move disk from " + startPeg + ' ' + endPeg
            SolveTowers(n - 1, tempPeg, endPeg, startPeg);

        }
    }
}