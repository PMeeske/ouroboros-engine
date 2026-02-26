namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ToolSelectionTests
{
    [Fact]
    public void Empty_HasNoTools()
    {
        var selection = ToolSelection.Empty;

        selection.HasTools.Should().BeFalse();
        selection.ConfidenceScore.Should().Be(0.0);
        selection.SelectedTools.Should().BeEmpty();
    }

    [Fact]
    public void Failed_SetsReasonAndZeroConfidence()
    {
        var selection = ToolSelection.Failed("No tools available");

        selection.HasTools.Should().BeFalse();
        selection.Reasoning.Should().Be("No tools available");
        selection.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public void Failed_ThrowsOnNull()
    {
        var act = () => ToolSelection.Failed(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
