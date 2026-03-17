using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ToolMatchTests
{
    [Fact]
    public void Create_WithoutCapabilities_ReturnsEmptyCapabilities()
    {
        // Act
        var match = ToolMatch.Create("my_tool", 0.75);

        // Assert
        match.ToolName.Should().Be("my_tool");
        match.RelevanceScore.Should().Be(0.75);
        match.MatchedCapabilities.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithCapabilities_SetsCapabilities()
    {
        // Act
        var match = ToolMatch.Create("tool", 0.9, new[] { "search", "summarize" });

        // Assert
        match.MatchedCapabilities.Should().HaveCount(2);
        match.MatchedCapabilities.Should().Contain("search");
        match.MatchedCapabilities.Should().Contain("summarize");
    }

    [Fact]
    public void Create_NullToolName_ThrowsArgumentNullException()
    {
        var act = () => ToolMatch.Create(null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithCapabilities_NullToolName_ThrowsArgumentNullException()
    {
        var act = () => ToolMatch.Create(null!, 0.5, Array.Empty<string>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullCapabilities_ThrowsArgumentNullException()
    {
        var act = () => ToolMatch.Create("tool", 0.5, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ScoreAboveOne_ClampsToOne()
    {
        // Act
        var match = ToolMatch.Create("tool", 1.5);

        // Assert
        match.RelevanceScore.Should().Be(1.0);
    }

    [Fact]
    public void Create_ScoreBelowZero_ClampsToZero()
    {
        // Act
        var match = ToolMatch.Create("tool", -0.5);

        // Assert
        match.RelevanceScore.Should().Be(0.0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var m1 = ToolMatch.Create("tool", 0.8);
        var m2 = ToolMatch.Create("tool", 0.8);

        m1.Should().Be(m2);
    }
}
