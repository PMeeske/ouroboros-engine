using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// Pre-loaded prompt templates for the four standard reasoning pipeline stages:
/// Thinking, Draft, Critique, and Improve.
/// Each template is loaded once from the embedded YAML resources at startup.
/// </summary>
public static class Prompts
{
    /// <summary>Prompt template for the initial thinking/exploration stage of a reasoning pipeline.</summary>
    public static readonly PromptTemplate Thinking =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Thinking"));

    /// <summary>Prompt template for generating a first draft given the thinking output.</summary>
    public static readonly PromptTemplate Draft =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Draft"));

    /// <summary>Prompt template for critiquing a draft for accuracy, completeness, and clarity.</summary>
    public static readonly PromptTemplate Critique =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Critique"));

    /// <summary>Prompt template for producing a refined response that addresses the critique feedback.</summary>
    public static readonly PromptTemplate Improve =
        new(PromptTemplateLoader.GetPromptText("Reasoning", "Improve"));
}
