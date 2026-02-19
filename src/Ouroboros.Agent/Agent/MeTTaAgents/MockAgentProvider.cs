// <copyright file="MockAgentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Mock agent provider for testing. Returns deterministic responses
/// based on the agent role, without requiring any external service.
/// </summary>
public sealed class MockAgentProvider : IAgentProviderFactory
{
    /// <inheritdoc/>
    public bool CanHandle(string providerName)
        => providerName is "LocalMock";

    /// <inheritdoc/>
    public Task<Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>> CreateModelAsync(
        MeTTaAgentDef agentDef, CancellationToken ct = default)
    {
        var model = new MockChatModel(agentDef.AgentId, agentDef.Role);
        return Task.FromResult(
            Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Success(model));
    }

    /// <inheritdoc/>
    public Task<Result<ProviderHealthStatus, string>> HealthCheckAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult(
            Result<ProviderHealthStatus, string>.Success(
                new ProviderHealthStatus("LocalMock", true, 0.0)));
    }
}

/// <summary>
/// A deterministic chat model for testing that returns role-appropriate responses.
/// </summary>
internal sealed class MockChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    private readonly string _agentId;
    private readonly string _role;

    public MockChatModel(string agentId, string role)
    {
        _agentId = agentId;
        _role = role;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        string response = _role switch
        {
            "Coder" => $"[mock-coder:{_agentId}] Implementation for: {Truncate(prompt, 100)}",
            "Reviewer" => $"[mock-reviewer:{_agentId}] Review complete. No issues found in: {Truncate(prompt, 100)}",
            "Planner" => $"[mock-planner:{_agentId}] Plan:\n1. Analyze requirements\n2. Implement solution\n3. Test and verify",
            "Reasoner" => $"[mock-reasoner:{_agentId}] Analysis: The logical conclusion for '{Truncate(prompt, 80)}' is consistent.",
            "Summarizer" => $"[mock-summarizer:{_agentId}] Summary: {Truncate(prompt, 120)}",
            _ => $"[mock-{_role.ToLowerInvariant()}:{_agentId}] Response to: {Truncate(prompt, 100)}"
        };

        return Task.FromResult(response);
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
