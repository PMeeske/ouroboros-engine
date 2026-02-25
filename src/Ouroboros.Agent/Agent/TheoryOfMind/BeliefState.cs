// <copyright file="BeliefState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents the beliefs attributed to another agent.
/// Tracks what the agent believes about the world and other agents.
/// </summary>
/// <param name="AgentId">The ID of the agent whose beliefs are modeled</param>
/// <param name="Beliefs">Dictionary of belief propositions and their values</param>
/// <param name="Confidence">Overall confidence in the belief model (0.0 to 1.0)</param>
/// <param name="LastUpdated">When the belief state was last updated</param>
public sealed record BeliefState(
    string AgentId,
    Dictionary<string, BeliefValue> Beliefs,
    double Confidence,
    DateTime LastUpdated)
{
    /// <summary>
    /// Creates an empty belief state for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <returns>Empty belief state with low confidence</returns>
    public static BeliefState Empty(string agentId) => new(
        agentId,
        new Dictionary<string, BeliefValue>(),
        0.0,
        DateTime.UtcNow);

    /// <summary>
    /// Adds or updates a belief in the state.
    /// </summary>
    /// <param name="key">The belief key</param>
    /// <param name="belief">The belief value</param>
    /// <returns>Updated belief state</returns>
    public BeliefState WithBelief(string key, BeliefValue belief)
    {
        Dictionary<string, BeliefValue> updated = new(this.Beliefs)
        {
            [key] = belief
        };

        return this with { Beliefs = updated, LastUpdated = DateTime.UtcNow };
    }

    /// <summary>
    /// Updates confidence in the belief model.
    /// </summary>
    /// <param name="newConfidence">New confidence value (0.0 to 1.0)</param>
    /// <returns>Updated belief state</returns>
    public BeliefState WithConfidence(double newConfidence) =>
        this with { Confidence = Math.Clamp(newConfidence, 0.0, 1.0) };
}