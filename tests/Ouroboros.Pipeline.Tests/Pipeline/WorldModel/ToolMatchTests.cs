namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ToolMatchTests
{
    [Fact]
    public void Create_WithNoCapabilities_HasEmptyList()
    {
        var match = ToolMatch.Create("search", 0.8);

        match.ToolName.Should().Be("search");
        match.RelevanceScore.Should().Be(0.8);
        match.MatchedCapabilities.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithCapabilities_SetsCapabilities()
    {
        var match = ToolMatch.Create("search", 0.8, new[] { "text-search", "fuzzy-match" });

        match.MatchedCapabilities.Should().HaveCount(2);
    }

    [Fact]
    public void Create_ClampsScore()
    {
        ToolMatch.Create("tool", 1.5).RelevanceScore.Should().Be(1.0);
        ToolMatch.Create("tool", -0.5).RelevanceScore.Should().Be(0.0);
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => ToolMatch.Create(null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }
}
