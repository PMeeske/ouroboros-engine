namespace Ouroboros.Tests.Pipeline.Ingestion;

using Ouroboros.Pipeline.Ingestion;

[Trait("Category", "Unit")]
public class DirectoryIngestionStatsTests
{
    [Fact]
    public void DefaultValues_AreZero()
    {
        var stats = new DirectoryIngestionStats();

        stats.FilesLoaded.Should().Be(0);
        stats.SkippedUnchanged.Should().Be(0);
        stats.Errors.Should().Be(0);
        stats.VectorsProduced.Should().Be(0);
        stats.Elapsed.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var stats = new DirectoryIngestionStats
        {
            FilesLoaded = 10,
            SkippedUnchanged = 3,
            Errors = 1,
            VectorsProduced = 50,
            Elapsed = TimeSpan.FromSeconds(5),
        };

        stats.FilesLoaded.Should().Be(10);
        stats.SkippedUnchanged.Should().Be(3);
        stats.Errors.Should().Be(1);
        stats.VectorsProduced.Should().Be(50);
        stats.Elapsed.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ToString_ContainsAllFields()
    {
        var stats = new DirectoryIngestionStats
        {
            FilesLoaded = 5,
            SkippedUnchanged = 2,
            Errors = 0,
            VectorsProduced = 20,
            Elapsed = TimeSpan.FromMilliseconds(1234),
        };

        var str = stats.ToString();

        str.Should().Contain("files=5");
        str.Should().Contain("skipped=2");
        str.Should().Contain("errors=0");
        str.Should().Contain("vectors=20");
    }
}
