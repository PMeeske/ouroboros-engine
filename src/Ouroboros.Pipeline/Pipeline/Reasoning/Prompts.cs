using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Reasoning;

public static class Prompts
{
    public static readonly PromptTemplate Thinking =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Thinking"));

    public static readonly PromptTemplate Draft =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Draft"));

    public static readonly PromptTemplate Critique =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Critique"));

    public static readonly PromptTemplate Improve =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Improve"));
}
