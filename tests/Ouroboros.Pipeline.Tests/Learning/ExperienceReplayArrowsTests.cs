using NSubstitute;
using Ouroboros.Pipeline.Learning;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Tests.Learning;

public class ExperienceReplayArrowsTests
{
    private readonly IExperienceBuffer _buffer = Substitute.For<IExperienceBuffer>();

    private static Experience CreateExperience() =>
        Experience.Create("state", "action", 0.5, "nextState");

    [Fact]
    public async Task AddExperienceArrow_WithValidExperience_ReturnsSuccess()
    {
        // Arrange
        var experience = CreateExperience();
        var step = ExperienceReplayArrows.AddExperienceArrow(_buffer);

        // Act
        var result = await step(experience);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _buffer.Received(1).Add(experience);
    }

    [Fact]
    public async Task AddExperienceArrow_WithNullExperience_ReturnsFailure()
    {
        // Arrange
        _buffer.When(b => b.Add(null!)).Throw(new ArgumentNullException("experience"));
        var step = ExperienceReplayArrows.AddExperienceArrow(_buffer);

        // Act
        var result = await step(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SampleExperiencesArrow_WithPositiveBatchSize_ReturnsSuccess()
    {
        // Arrange
        var experiences = new List<Experience> { CreateExperience() };
        _buffer.Count.Returns(5);
        _buffer.Sample(3).Returns(experiences);
        var step = ExperienceReplayArrows.SampleExperiencesArrow(_buffer);

        // Act
        var result = await step(3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task SampleExperiencesArrow_WithZeroBatchSize_ReturnsFailure()
    {
        // Arrange
        var step = ExperienceReplayArrows.SampleExperiencesArrow(_buffer);

        // Act
        var result = await step(0);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SampleExperiencesArrow_WithEmptyBuffer_ReturnsFailure()
    {
        // Arrange
        _buffer.Count.Returns(0);
        var step = ExperienceReplayArrows.SampleExperiencesArrow(_buffer);

        // Act
        var result = await step(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task SamplePrioritizedArrow_WithPositiveBatchSize_ReturnsSuccess()
    {
        // Arrange
        var experiences = new List<Experience> { CreateExperience() };
        _buffer.Count.Returns(5);
        _buffer.SamplePrioritized(3, 0.6).Returns(experiences);
        var step = ExperienceReplayArrows.SamplePrioritizedArrow(_buffer);

        // Act
        var result = await step(3);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SamplePrioritizedArrow_WithEmptyBuffer_ReturnsFailure()
    {
        // Arrange
        _buffer.Count.Returns(0);
        var step = ExperienceReplayArrows.SamplePrioritizedArrow(_buffer);

        // Act
        var result = await step(5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePriorityArrow_WithExistingId_ReturnsSuccess()
    {
        // Arrange
        var id = Guid.NewGuid();
        _buffer.UpdatePriority(id, 5.0).Returns(true);
        var step = ExperienceReplayArrows.UpdatePriorityArrow(_buffer);

        // Act
        var result = await step((id, 5.0));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePriorityArrow_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _buffer.UpdatePriority(id, 5.0).Returns(false);
        var step = ExperienceReplayArrows.UpdatePriorityArrow(_buffer);

        // Act
        var result = await step((id, 5.0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RecordExperienceArrow_CreatesAndStoresExperience()
    {
        // Arrange
        var step = ExperienceReplayArrows.RecordExperienceArrow(_buffer);

        // Act
        var result = await step(("state", "action", 0.5, "next"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be("state");
        result.Value.Action.Should().Be("action");
        result.Value.Reward.Should().Be(0.5);
        _buffer.Received(1).Add(Arg.Any<Experience>());
    }

    [Fact]
    public async Task ClearBufferArrow_ClearsBuffer()
    {
        // Arrange
        var step = ExperienceReplayArrows.ClearBufferArrow(_buffer);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _buffer.Received(1).Clear();
    }

    [Fact]
    public async Task GetBufferStatsArrow_ReturnsCountAndCapacity()
    {
        // Arrange
        _buffer.Count.Returns(42);
        _buffer.Capacity.Returns(1000);
        var step = ExperienceReplayArrows.GetBufferStatsArrow(_buffer);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.Count.Should().Be(42);
        result.Capacity.Should().Be(1000);
    }
}
