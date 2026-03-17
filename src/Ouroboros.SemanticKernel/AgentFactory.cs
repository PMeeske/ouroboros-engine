// <copyright file="AgentFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// Factory for creating Semantic Kernel <see cref="ChatCompletionAgent"/> instances
/// and composing them into multi-agent conversations via <see cref="AgentGroupChat"/>.
/// <para>
/// This is the foundation for replacing ConsolidatedMind's manual multi-role routing
/// with SK's native agent orchestration. Each agent wraps a <see cref="Kernel"/>
/// (and therefore an Ouroboros chat provider) with a distinct persona and instruction set.
/// </para>
/// </summary>
public sealed class AgentFactory
{
    private readonly Kernel _kernel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFactory"/> class.
    /// </summary>
    /// <param name="kernel">
    /// The shared <see cref="Kernel"/> instance that agents will use for chat completion.
    /// Each agent gets a clone of this kernel to ensure isolation.
    /// </param>
    public AgentFactory(Kernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
    }

    /// <summary>
    /// Creates a single <see cref="ChatCompletionAgent"/> with the given persona.
    /// </summary>
    /// <param name="name">A short identifier for the agent (e.g. "Analyst", "Critic").</param>
    /// <param name="instructions">
    /// The system prompt / persona instructions that guide this agent's behavior.
    /// </param>
    /// <returns>A configured <see cref="ChatCompletionAgent"/>.</returns>
    public ChatCompletionAgent CreateAgent(string name, string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);

        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = instructions,
            Kernel = _kernel.Clone(),
        };
    }

    /// <summary>
    /// Creates an <see cref="AgentGroupChat"/> that orchestrates a round-robin
    /// conversation between the supplied agents. The group chat terminates when
    /// the agents reach consensus or the caller cancels.
    /// </summary>
    /// <param name="agents">Two or more agents to participate in the group chat.</param>
    /// <returns>An <see cref="AgentGroupChat"/> ready to invoke.</returns>
    public static AgentGroupChat CreateGroupChat(params ChatCompletionAgent[] agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (agents.Length < 2)
        {
            throw new ArgumentException(
                "At least two agents are required for a group chat.", nameof(agents));
        }

        return new AgentGroupChat(agents);
    }
}
