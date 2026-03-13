// <copyright file="UserAdvocateAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// The User Advocate agent represents end-user perspective, usability, and accessibility.
/// </summary>
public sealed class UserAdvocateAgent : BaseAgentPersona
{
    /// <inheritdoc />
    public override string Name => "UserAdvocate";

    /// <inheritdoc />
    public override string Description =>
        "Represents end-user perspective, usability, and accessibility. " +
        "Ensures solutions serve the people who will actually use them.";

    /// <inheritdoc />
    public override double ExpertiseWeight => 0.9;

    /// <inheritdoc />
    public override string SystemPrompt =>
        PromptTemplateLoader.GetPromptText("Council", "UserAdvocate");
}
