namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class TheoristAgentTests
{
    [Fact]
    public void Name_ReturnsTheorist() => new TheoristAgent().Name.Should().Be("Theorist");

    [Fact]
    public void ExpertiseWeight_IsPointEightFive() => new TheoristAgent().ExpertiseWeight.Should().Be(0.85);

    [Fact]
    public void SystemPrompt_ContainsTheoristRole() =>
        new TheoristAgent().SystemPrompt.Should().Contain("Theorist");
}
