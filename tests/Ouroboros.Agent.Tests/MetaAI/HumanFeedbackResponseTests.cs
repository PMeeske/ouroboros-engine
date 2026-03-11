using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class HumanFeedbackResponseTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { { "source", "ui" } };
        var respondedAt = DateTime.UtcNow;

        // Act
        var sut = new HumanFeedbackResponse("req-001", "Looks good", metadata, respondedAt);

        // Assert
        sut.RequestId.Should().Be("req-001");
        sut.Response.Should().Be("Looks good");
        sut.Metadata.Should().ContainKey("source");
        sut.RespondedAt.Should().Be(respondedAt);
    }

    [Fact]
    public void Constructor_WithNullMetadata_ShouldWork()
    {
        // Arrange & Act
        var sut = new HumanFeedbackResponse("req-002", "Rejected", null, DateTime.UtcNow);

        // Assert
        sut.Metadata.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var a = new HumanFeedbackResponse("r1", "ok", null, now);
        var b = new HumanFeedbackResponse("r1", "ok", null, now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new HumanFeedbackResponse("r1", "ok", null, DateTime.UtcNow);

        // Act
        var modified = original with { Response = "not ok" };

        // Assert
        modified.Response.Should().Be("not ok");
        modified.RequestId.Should().Be("r1");
    }
}
