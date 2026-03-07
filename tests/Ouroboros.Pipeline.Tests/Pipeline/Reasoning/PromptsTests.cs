namespace Ouroboros.Tests.Pipeline.Reasoning;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class PromptsTests
{
    [Fact]
    public void Thinking_HasRequiredVariables()
    {
        Prompts.Thinking.RequiredVariables.Should().Contain("tools_schemas");
        Prompts.Thinking.RequiredVariables.Should().Contain("context");
        Prompts.Thinking.RequiredVariables.Should().Contain("topic");
    }

    [Fact]
    public void Draft_HasRequiredVariables()
    {
        Prompts.Draft.RequiredVariables.Should().Contain("tools_schemas");
        Prompts.Draft.RequiredVariables.Should().Contain("context");
        Prompts.Draft.RequiredVariables.Should().Contain("topic");
    }

    [Fact]
    public void Critique_HasDraftVariable()
    {
        Prompts.Critique.RequiredVariables.Should().Contain("draft");
    }

    [Fact]
    public void Improve_HasCritiqueAndDraftVariables()
    {
        Prompts.Improve.RequiredVariables.Should().Contain("draft");
        Prompts.Improve.RequiredVariables.Should().Contain("critique");
    }
}
