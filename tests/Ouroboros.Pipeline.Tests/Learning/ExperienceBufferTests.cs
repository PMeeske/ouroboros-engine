using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class ExperienceBufferTests
{
    private static Experience CreateExperience(double reward = 0.5, double priority = 1.0) =>
        Experience.Create("state", "action", reward, "nextState", priority);

    [Fact]
    public void Constructor_WithDefaultCapacity_SetsCapacity()
    {
        // Act
        var buffer = new ExperienceBuffer();

        // Assert
        buffer.Capacity.Should().Be(10000);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithCustomCapacity_SetsCapacity()
    {
        // Act
        var buffer = new ExperienceBuffer(capacity: 100);

        // Assert
        buffer.Capacity.Should().Be(100);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => new ExperienceBuffer(capacity: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => new ExperienceBuffer(capacity: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);

        // Act
        buffer.Add(CreateExperience());

        // Assert
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public void Add_WithNullExperience_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new ExperienceBuffer();

        // Act & Assert
        var act = () => buffer.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_AtCapacity_EvictsOldestExperience()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 3, seed: 42);
        var exp1 = CreateExperience(0.1);
        var exp2 = CreateExperience(0.2);
        var exp3 = CreateExperience(0.3);
        var exp4 = CreateExperience(0.4);

        // Act
        buffer.Add(exp1);
        buffer.Add(exp2);
        buffer.Add(exp3);
        buffer.Add(exp4); // Should evict exp1

        // Assert
        buffer.Count.Should().Be(3);
        var all = buffer.GetAll();
        all.Should().NotContain(exp1);
        all.Should().Contain(exp4);
    }

    [Fact]
    public void Sample_WithZeroBatchSize_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        buffer.Add(CreateExperience());

        // Act
        var result = buffer.Sample(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sample_WithNegativeBatchSize_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        buffer.Add(CreateExperience());

        // Act
        var result = buffer.Sample(-1);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sample_FromEmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer();

        // Act
        var result = buffer.Sample(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sample_ReturnsRequestedBatchSize()
    {
        // Arrange
        var buffer = new ExperienceBuffer(seed: 42);
        for (int i = 0; i < 20; i++)
            buffer.Add(CreateExperience(i * 0.1));

        // Act
        var result = buffer.Sample(5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public void Sample_WhenBatchSizeExceedsCount_ReturnsAllExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer(seed: 42);
        buffer.Add(CreateExperience(0.1));
        buffer.Add(CreateExperience(0.2));

        // Act
        var result = buffer.Sample(10);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Sample_ReturnsUniqueExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer(seed: 42);
        for (int i = 0; i < 20; i++)
            buffer.Add(CreateExperience(i * 0.05));

        // Act
        var result = buffer.Sample(10);

        // Assert
        result.Select(e => e.Id).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public void SamplePrioritized_WithZeroBatchSize_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        buffer.Add(CreateExperience());

        // Act
        var result = buffer.SamplePrioritized(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SamplePrioritized_FromEmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer();

        // Act
        var result = buffer.SamplePrioritized(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SamplePrioritized_ReturnsRequestedBatchSize()
    {
        // Arrange
        var buffer = new ExperienceBuffer(seed: 42);
        for (int i = 0; i < 20; i++)
            buffer.Add(CreateExperience(i * 0.1, priority: i + 1));

        // Act
        var result = buffer.SamplePrioritized(5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public void Clear_RemovesAllExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        buffer.Add(CreateExperience());
        buffer.Add(CreateExperience());

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void UpdatePriority_WithExistingId_ReturnsTrue()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        var exp = CreateExperience();
        buffer.Add(exp);

        // Act
        var result = buffer.UpdatePriority(exp.Id, 5.0);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void UpdatePriority_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        buffer.Add(CreateExperience());

        // Act
        var result = buffer.UpdatePriority(Guid.NewGuid(), 5.0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdatePriority_UpdatesThePriority()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        var exp = CreateExperience(priority: 1.0);
        buffer.Add(exp);

        // Act
        buffer.UpdatePriority(exp.Id, 10.0);

        // Assert
        var all = buffer.GetAll();
        all[0].Priority.Should().Be(10.0);
    }

    [Fact]
    public void GetAll_ReturnsAllExperiencesInOrder()
    {
        // Arrange
        var buffer = new ExperienceBuffer();
        var exp1 = CreateExperience(0.1);
        var exp2 = CreateExperience(0.2);
        buffer.Add(exp1);
        buffer.Add(exp2);

        // Act
        var all = buffer.GetAll();

        // Assert
        all.Should().HaveCount(2);
        all[0].Reward.Should().Be(0.1);
        all[1].Reward.Should().Be(0.2);
    }
}
