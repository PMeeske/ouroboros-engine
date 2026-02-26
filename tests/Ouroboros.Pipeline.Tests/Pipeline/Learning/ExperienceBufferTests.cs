namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class ExperienceBufferTests
{
    [Fact]
    public void Constructor_InitializesWithCapacity()
    {
        var buffer = new ExperienceBuffer(10);

        buffer.Count.Should().Be(0);
        buffer.Capacity.Should().Be(10);
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        var buffer = new ExperienceBuffer(10);
        buffer.Add(Experience.Create("s1", "a1", 0.5, "s2"));

        buffer.Count.Should().Be(1);
    }

    [Fact]
    public void Sample_ReturnsBatchOfRequestedSize()
    {
        var buffer = new ExperienceBuffer(100);
        for (int i = 0; i < 20; i++)
        {
            buffer.Add(Experience.Create($"s{i}", $"a{i}", 0.5, $"s{i + 1}"));
        }

        var sample = buffer.Sample(5);
        sample.Should().HaveCount(5);
    }

    [Fact]
    public void Sample_ReturnsAllWhenRequestExceedsCount()
    {
        var buffer = new ExperienceBuffer(100);
        buffer.Add(Experience.Create("s1", "a1", 0.5, "s2"));

        var sample = buffer.Sample(10);
        sample.Should().HaveCount(1);
    }

    [Fact]
    public void Clear_RemovesAllExperiences()
    {
        var buffer = new ExperienceBuffer(10);
        buffer.Add(Experience.Create("s1", "a1", 0.5, "s2"));
        buffer.Clear();

        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void GetAll_ReturnsAllExperiences()
    {
        var buffer = new ExperienceBuffer(10);
        buffer.Add(Experience.Create("s1", "a1", 0.5, "s2"));
        buffer.Add(Experience.Create("s2", "a2", 0.8, "s3"));

        buffer.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void SamplePrioritized_FavorsHigherPriorityExperiences()
    {
        var buffer = new ExperienceBuffer(100);
        for (int i = 0; i < 50; i++)
        {
            var exp = Experience.Create($"s{i}", $"a{i}", 0.5, $"s{i + 1}")
                .WithTDErrorPriority(i * 0.02);
            buffer.Add(exp);
        }

        var sample = buffer.SamplePrioritized(5);
        sample.Should().HaveCount(5);
    }
}
