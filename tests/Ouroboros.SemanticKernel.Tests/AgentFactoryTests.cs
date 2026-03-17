// <copyright file="AgentFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Ouroboros.SemanticKernel.Tests;

public sealed class AgentFactoryTests
{
    private readonly Kernel _kernel = Kernel.CreateBuilder().Build();

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullKernel_ThrowsArgumentNullException()
    {
        var act = () => new AgentFactory(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("kernel");
    }

    [Fact]
    public void Constructor_ValidKernel_DoesNotThrow()
    {
        var act = () => new AgentFactory(_kernel);

        act.Should().NotThrow();
    }

    // ── CreateAgent ──────────────────────────────────────────────────────

    [Fact]
    public void CreateAgent_NullName_ThrowsArgumentException()
    {
        var sut = new AgentFactory(_kernel);

        var act = () => sut.CreateAgent(null!, "You are a helpful assistant.");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void CreateAgent_WhitespaceName_ThrowsArgumentException()
    {
        var sut = new AgentFactory(_kernel);

        var act = () => sut.CreateAgent("   ", "You are a helpful assistant.");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void CreateAgent_NullInstructions_ThrowsArgumentException()
    {
        var sut = new AgentFactory(_kernel);

        var act = () => sut.CreateAgent("Analyst", null!);

        act.Should().Throw<ArgumentException>().WithParameterName("instructions");
    }

    [Fact]
    public void CreateAgent_WhitespaceInstructions_ThrowsArgumentException()
    {
        var sut = new AgentFactory(_kernel);

        var act = () => sut.CreateAgent("Analyst", "   ");

        act.Should().Throw<ArgumentException>().WithParameterName("instructions");
    }

    [Fact]
    public void CreateAgent_ValidInputs_ReturnsChatCompletionAgent()
    {
        var sut = new AgentFactory(_kernel);

        var agent = sut.CreateAgent("Analyst", "Analyze the data.");

        agent.Should().NotBeNull();
        agent.Should().BeOfType<ChatCompletionAgent>();
    }

    [Fact]
    public void CreateAgent_SetsNameAndInstructions()
    {
        var sut = new AgentFactory(_kernel);

        var agent = sut.CreateAgent("Critic", "Critique the proposal.");

        agent.Name.Should().Be("Critic");
        agent.Instructions.Should().Be("Critique the proposal.");
    }

    [Fact]
    public void CreateAgent_ClonesKernel()
    {
        var sut = new AgentFactory(_kernel);

        var agent = sut.CreateAgent("Analyst", "Analyze the data.");

        agent.Kernel.Should().NotBeSameAs(_kernel);
    }

    // ── CreateGroupChat ──────────────────────────────────────────────────

    [Fact]
    public void CreateGroupChat_NullAgents_ThrowsArgumentNullException()
    {
        var act = () => AgentFactory.CreateGroupChat(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("agents");
    }

    [Fact]
    public void CreateGroupChat_SingleAgent_ThrowsArgumentException()
    {
        var agent = CreateTestAgent("Agent1");

        var act = () => AgentFactory.CreateGroupChat(agent);

        act.Should().Throw<ArgumentException>().WithParameterName("agents");
    }

    [Fact]
    public void CreateGroupChat_EmptyAgents_ThrowsArgumentException()
    {
        var act = () => AgentFactory.CreateGroupChat();

        act.Should().Throw<ArgumentException>().WithParameterName("agents");
    }

    [Fact]
    public void CreateGroupChat_TwoAgents_ReturnsAgentGroupChat()
    {
        var agent1 = CreateTestAgent("Agent1");
        var agent2 = CreateTestAgent("Agent2");

        var groupChat = AgentFactory.CreateGroupChat(agent1, agent2);

        groupChat.Should().NotBeNull();
        groupChat.Should().BeOfType<AgentGroupChat>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private ChatCompletionAgent CreateTestAgent(string name)
    {
        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = "Test instructions.",
            Kernel = _kernel.Clone(),
        };
    }
}
