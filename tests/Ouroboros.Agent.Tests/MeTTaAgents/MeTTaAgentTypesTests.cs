// <copyright file="MeTTaAgentTypesTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MeTTaAgents;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MeTTaAgentTypesTests
{
    [Fact]
    public void MeTTaAgentDef_RecordProperties()
    {
        var def = new MeTTaAgentDef("a1", "Ollama", "llama3", "Coder",
            "You are a coder.", 4096, 0.5f, "http://localhost:11434", null,
            new List<string> { "coding" });

        def.AgentId.Should().Be("a1");
        def.Provider.Should().Be("Ollama");
        def.Model.Should().Be("llama3");
        def.Role.Should().Be("Coder");
        def.SystemPrompt.Should().Be("You are a coder.");
        def.MaxTokens.Should().Be(4096);
        def.Temperature.Should().Be(0.5f);
        def.Endpoint.Should().Be("http://localhost:11434");
        def.Capabilities.Should().Contain("coding");
    }

    [Fact]
    public void ProviderHealthStatus_RecordProperties()
    {
        var status = new ProviderHealthStatus("Ollama", true, 42.5, null);

        status.ProviderName.Should().Be("Ollama");
        status.IsHealthy.Should().BeTrue();
        status.LatencyMs.Should().Be(42.5);
        status.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AgentOperationStatus_RecordProperties()
    {
        var now = DateTime.UtcNow;
        var status = new AgentOperationStatus("a1", "Active", "Running", now);

        status.AgentId.Should().Be("a1");
        status.Status.Should().Be("Active");
        status.Message.Should().Be("Running");
        status.Timestamp.Should().Be(now);
    }

    [Theory]
    [InlineData("Coder", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.CodeExpert)]
    [InlineData("Reviewer", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Verifier)]
    [InlineData("Planner", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Planner)]
    [InlineData("Reasoner", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.DeepReasoning)]
    [InlineData("Researcher", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Analyst)]
    [InlineData("Summarizer", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Synthesizer)]
    [InlineData("SecurityAuditor", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Analyst)]
    [InlineData("Critic", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Verifier)]
    [InlineData("Synthesizer", Ouroboros.Agent.ConsolidatedMind.SpecializedRole.Synthesizer)]
    public void AgentRoleMapping_MapsKnownRoles(string mettaRole, Ouroboros.Agent.ConsolidatedMind.SpecializedRole expected)
    {
        AgentRoleMapping.ToSpecializedRole(mettaRole).Should().Be(expected);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("CustomRole")]
    public void AgentRoleMapping_ReturnsNullForUnknownRoles(string role)
    {
        AgentRoleMapping.ToSpecializedRole(role).Should().BeNull();
    }
}
