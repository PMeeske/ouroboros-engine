// <copyright file="AgentModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents a complete model of another agent.
/// Includes beliefs, goals, capabilities, and personality traits.
/// </summary>
/// <param name="AgentId">The ID of the modeled agent</param>
/// <param name="Beliefs">The agent's belief state</param>
/// <param name="InferredGoals">Inferred goals and objectives</param>
/// <param name="InferredCapabilities">Inferred capabilities and skills</param>
/// <param name="Personality">Personality trait assessments</param>
/// <param name="ObservationHistory">History of observations used to build this model</param>
/// <param name="CreatedAt">When this model was first created</param>
/// <param name="LastInteraction">When the last interaction occurred</param>
public sealed record AgentModel(
    string AgentId,
    BeliefState Beliefs,
    List<string> InferredGoals,
    List<string> InferredCapabilities,
    PersonalityTraits Personality,
    List<AgentObservation> ObservationHistory,
    DateTime CreatedAt,
    DateTime LastInteraction)
{
    /// <summary>
    /// Creates a new agent model from initial observations.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <returns>New agent model with empty state</returns>
    public static AgentModel Create(string agentId) => new(
        agentId,
        BeliefState.Empty(agentId),
        new List<string>(),
        new List<string>(),
        PersonalityTraits.Default(),
        new List<AgentObservation>(),
        DateTime.UtcNow,
        DateTime.UtcNow);

    /// <summary>
    /// Updates the model with a new observation.
    /// </summary>
    /// <param name="observation">The new observation</param>
    /// <returns>Updated agent model</returns>
    public AgentModel WithObservation(AgentObservation observation)
    {
        List<AgentObservation> updatedHistory = new(this.ObservationHistory)
        {
            observation
        };

        return this with
        {
            ObservationHistory = updatedHistory,
            LastInteraction = observation.ObservedAt
        };
    }

    /// <summary>
    /// Updates beliefs in the model.
    /// </summary>
    /// <param name="beliefs">New belief state</param>
    /// <returns>Updated agent model</returns>
    public AgentModel WithBeliefs(BeliefState beliefs) =>
        this with { Beliefs = beliefs };

    /// <summary>
    /// Adds an inferred goal to the model.
    /// </summary>
    /// <param name="goal">The inferred goal</param>
    /// <returns>Updated agent model</returns>
    public AgentModel WithGoal(string goal)
    {
        if (this.InferredGoals.Contains(goal))
            return this;

        List<string> updatedGoals = new(this.InferredGoals) { goal };
        return this with { InferredGoals = updatedGoals };
    }

    /// <summary>
    /// Adds an inferred capability to the model.
    /// </summary>
    /// <param name="capability">The inferred capability</param>
    /// <returns>Updated agent model</returns>
    public AgentModel WithCapability(string capability)
    {
        if (this.InferredCapabilities.Contains(capability))
            return this;

        List<string> updatedCapabilities = new(this.InferredCapabilities) { capability };
        return this with { InferredCapabilities = updatedCapabilities };
    }

    /// <summary>
    /// Updates personality traits.
    /// </summary>
    /// <param name="personality">New personality traits</param>
    /// <returns>Updated agent model</returns>
    public AgentModel WithPersonality(PersonalityTraits personality) =>
        this with { Personality = personality };
}