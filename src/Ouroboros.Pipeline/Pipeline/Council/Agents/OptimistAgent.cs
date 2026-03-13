// <copyright file="OptimistAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// The Optimist agent focuses on possibilities, creative solutions, and positive outcomes.
/// </summary>
public sealed class OptimistAgent : BaseAgentPersona
{
    /// <inheritdoc />
    public override string Name => "Optimist";

    /// <inheritdoc />
    public override string Description =>
        "Focuses on possibilities, creative solutions, and positive outcomes. " +
        "Sees opportunities where others see obstacles and champions innovative approaches.";

    /// <inheritdoc />
    public override double ExpertiseWeight => 0.9;

    /// <inheritdoc />
    public override string SystemPrompt =>
        PromptTemplateLoader.GetPromptText("Council", "Optimist");
}
