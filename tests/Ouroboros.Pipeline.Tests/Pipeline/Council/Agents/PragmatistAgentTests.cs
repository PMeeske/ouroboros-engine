namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class PragmatistAgentTests
{
    [Fact]
    public void Name_ReturnsPragmatist() => new PragmatistAgent().Name.Should().Be("Pragmatist");

    [Fact]
    public void ExpertiseWeight_IsPointNineFive() => new PragmatistAgent().ExpertiseWeight.Should().Be(0.95);

    [Fact]
    public void SystemPrompt_ContainsPragmatistRole() =>
        new PragmatistAgent().SystemPrompt.Should().Contain("Pragmatist");
}
