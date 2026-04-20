using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

/// <summary>
/// Shared helpers for delegation strategy tests.
/// </summary>
internal static class StrategyTestHelpers
{
    public static AgentIdentity CreateIdentity(string name, AgentRole role, params (string Name, double Proficiency)[] capabilities)
    {
        var identity = AgentIdentity.Create(name, role);
        foreach (var (capName, proficiency) in capabilities)
        {
            identity = identity.WithCapability(AgentCapability.Create(capName, $"{capName} capability", proficiency));
        }
        return identity;
    }

    public static AgentTeam CreateTeamWithAgents(params AgentIdentity[] identities)
    {
        var team = AgentTeam.Empty;
        foreach (var identity in identities)
        {
            team = team.AddAgent(identity);
        }
        return team;
    }

    public static DelegationCriteria CreateCriteria(
        string? requiredCapability = null,
        AgentRole? preferredRole = null,
        double minProficiency = 0.0)
    {
        var goal = Goal.Atomic("Test goal");
        var criteria = DelegationCriteria.FromGoal(goal);

        if (requiredCapability != null)
            criteria = criteria.RequireCapability(requiredCapability);

        if (preferredRole.HasValue)
            criteria = criteria.WithPreferredRole(preferredRole.Value);

        if (minProficiency > 0.0)
            criteria = criteria.WithMinProficiency(minProficiency);

        return criteria;
    }
}
