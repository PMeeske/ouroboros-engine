namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// A dynamically created DSL token from a learned skill.
/// </summary>
public sealed class DynamicSkillToken
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _model;

    /// <summary>
    /// Initializes a dynamic skill token.
    /// </summary>
    public DynamicSkillToken(Skill skill, Ouroboros.Abstractions.Core.IChatCompletionModel model)
    {
        ArgumentNullException.ThrowIfNull(skill);
        Skill = skill;
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <summary>
    /// Gets the underlying skill.
    /// </summary>
    public Skill Skill { get; }

    /// <summary>
    /// Executes this skill with the given input.
    /// </summary>
    public async Task<string> ExecuteAsync(string input, string? args)
    {
        string currentOutput = input;

        foreach (PlanStep planStep in Skill.Steps)
        {
            string stepPrompt = $@"Execute this step: {planStep.Action}

Input: {currentOutput}
Expected output: {planStep.ExpectedOutcome}

Perform this step and return the result.";

            string stepResult = await _model.GenerateTextAsync(stepPrompt).ConfigureAwait(false);
            currentOutput = stepResult;
        }

        return currentOutput;
    }
}