using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class MinorityOpinionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var concerns = new List<string> { "Security risk", "Performance impact" };

        // Act
        var opinion = new MinorityOpinion(
            "SecurityCynic",
            "REJECT",
            "Too many vulnerabilities",
            concerns);

        // Assert
        opinion.AgentName.Should().Be("SecurityCynic");
        opinion.Position.Should().Be("REJECT");
        opinion.Rationale.Should().Be("Too many vulnerabilities");
        opinion.Concerns.Should().HaveCount(2);
        opinion.Concerns.Should().Contain("Security risk");
        opinion.Concerns.Should().Contain("Performance impact");
    }

    [Fact]
    public void Constructor_WithEmptyConcerns_IsValid()
    {
        // Act
        var opinion = new MinorityOpinion("Agent", "ABSTAIN", "No strong opinion", []);

        // Assert
        opinion.Concerns.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        IReadOnlyList<string> concerns = ["concern1"];

        // Act
        var opinion1 = new MinorityOpinion("Agent", "REJECT", "Reason", concerns);
        var opinion2 = new MinorityOpinion("Agent", "REJECT", "Reason", concerns);

        // Assert
        opinion1.Should().Be(opinion2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new MinorityOpinion("Agent", "REJECT", "Reason", ["concern"]);

        // Act
        var modified = original with { Position = "ABSTAIN" };

        // Assert
        modified.AgentName.Should().Be("Agent");
        modified.Position.Should().Be("ABSTAIN");
        modified.Rationale.Should().Be("Reason");
    }
}
