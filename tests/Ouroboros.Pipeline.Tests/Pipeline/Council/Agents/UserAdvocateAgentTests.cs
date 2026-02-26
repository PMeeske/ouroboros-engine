namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class UserAdvocateAgentTests
{
    [Fact]
    public void Name_ReturnsUserAdvocate() => new UserAdvocateAgent().Name.Should().Be("UserAdvocate");

    [Fact]
    public void ExpertiseWeight_IsPointNine() => new UserAdvocateAgent().ExpertiseWeight.Should().Be(0.9);

    [Fact]
    public void SystemPrompt_ContainsUserAdvocateRole() =>
        new UserAdvocateAgent().SystemPrompt.Should().Contain("User Advocate");
}
