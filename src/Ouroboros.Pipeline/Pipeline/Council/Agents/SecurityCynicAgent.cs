// <copyright file="SecurityCynicAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// The Security Cynic agent emphasizes risks, vulnerabilities, and potential failures.
/// </summary>
public sealed class SecurityCynicAgent : BaseAgentPersona
{
    /// <inheritdoc />
    public override string Name => "SecurityCynic";

    /// <inheritdoc />
    public override string Description =>
        "Emphasizes risks, vulnerabilities, and potential failures. " +
        "Serves as the skeptical voice that ensures thorough vetting of proposals.";

    /// <inheritdoc />
    public override double ExpertiseWeight => 1.0;

    /// <inheritdoc />
    public override string SystemPrompt =>
        PromptTemplateLoader.GetPromptText("Council", "SecurityCynic");
}
