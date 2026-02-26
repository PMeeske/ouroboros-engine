namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class SecurityCynicAgentTests
{
    [Fact]
    public void Name_ReturnsSecurityCynic() => new SecurityCynicAgent().Name.Should().Be("SecurityCynic");

    [Fact]
    public void ExpertiseWeight_IsOne() => new SecurityCynicAgent().ExpertiseWeight.Should().Be(1.0);

    [Fact]
    public void SystemPrompt_ContainsSecurityContext() =>
        new SecurityCynicAgent().SystemPrompt.Should().Contain("Security Cynic");
}
