// <copyright file="TheoristAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// The Theorist agent analyzes mathematical correctness, formal properties, and theoretical soundness.
/// </summary>
public sealed class TheoristAgent : BaseAgentPersona
{
    /// <inheritdoc />
    public override string Name => "Theorist";

    /// <inheritdoc />
    public override string Description =>
        "Analyzes mathematical correctness, formal properties, and theoretical soundness. " +
        "Ensures proposals are logically consistent and well-founded.";

    /// <inheritdoc />
    public override double ExpertiseWeight => 0.85;

    /// <inheritdoc />
    public override string SystemPrompt =>
        PromptTemplateLoader.GetPromptText("Council", "Theorist");
}
