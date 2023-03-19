namespace jplground.CategorizedQueue.Tests;

public class KeyValueArrayMapTests
{
    [Fact]
    public void GivenASemaphoreMap_AndItsRotated_TheResultsAreCorrect()
    {
        var data = new (Guid Name, string Value)[] 
        {
            (Guid.NewGuid(), "1"),
            (Guid.NewGuid(), "2"),
            (Guid.NewGuid(), "3"),
            (Guid.NewGuid(), "4"),
        };
        var semaphoreMap = new KeyValueArrayMap<Guid, string>(data);
        semaphoreMap.Values.Should().ContainInOrder(new [] {"1", "2", "3", "4"});
        for(int i = 0; i < data.Length; ++i)
        {
            semaphoreMap.CategoryFromIndex(i).Should().Be(data[i].Name);
        }

        semaphoreMap.Rotate();
        semaphoreMap.Values.Should().ContainInOrder(new [] {"2", "3", "4", "1"});
        semaphoreMap.CategoryFromIndex(0).Should().Be(data[1].Name);
        semaphoreMap.CategoryFromIndex(1).Should().Be(data[2].Name);
        semaphoreMap.CategoryFromIndex(2).Should().Be(data[3].Name);
        semaphoreMap.CategoryFromIndex(3).Should().Be(data[0].Name);
    }

    [Fact]
    public void GivenASemaphoreMap_AndItsRotatedTheNumberOfTimesAsTheContent_TheResultsReturn()
    {
        var data = Enumerable.Range(0, 1000).Select<int, (Guid Name, string Value)>(i => new (Guid.NewGuid(), i.ToString())).ToArray();
        var values = data.Select(d => d.Value).ToArray();

        var semaphoreMap = new KeyValueArrayMap<Guid, string>(data);

        for(var i  = 0; i < data.Length; ++i)
        {
            semaphoreMap.Rotate();
        }

        semaphoreMap.Values.Should().ContainInOrder(values);
        for(int i = 0; i < data.Length; ++i)
        {
            semaphoreMap.CategoryFromIndex(i).Should().Be(data[i].Name);
        }
    }
}
