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
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        _model = model ?? throw new ArgumentNullException(nameof(model));
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
        // Execute each step in the skill
        var context = new Dictionary<string, string>
        {
            ["input"] = input,
            ["args"] = args ?? string.Empty,
        };

        string currentOutput = input;

        foreach (PlanStep planStep in Skill.Steps)
        {
            string stepPrompt = $@"Execute this step: {planStep.Action}

Input: {currentOutput}
Expected output: {planStep.ExpectedOutcome}

Perform this step and return the result.";

            string stepResult = await _model.GenerateTextAsync(stepPrompt);
            currentOutput = stepResult;
        }

        return currentOutput;
    }
}