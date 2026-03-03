// <copyright file="PragmatistAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// The Pragmatist agent evaluates feasibility, resource constraints, and practical considerations.
/// </summary>
public sealed class PragmatistAgent : BaseAgentPersona
{
    /// <inheritdoc />
    public override string Name => "Pragmatist";

    /// <inheritdoc />
    public override string Description =>
        "Evaluates feasibility, resource constraints, and practical considerations. " +
        "Focuses on what can realistically be achieved with available resources.";

    /// <inheritdoc />
    public override double ExpertiseWeight => 0.95;

    /// <inheritdoc />
    public override string SystemPrompt =>
        PromptTemplateLoader.GetPromptText("Council", "Pragmatist");
}
