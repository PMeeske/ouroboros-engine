// ==========================================================
// Needs Hierarchy — Maslow's hierarchy for agent motivation
// Phase 3: Affective Dynamics - Need-based drive prioritization
// Lower needs block higher needs when satisfaction drops below threshold
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// The five levels of Maslow's hierarchy of needs, adapted for AI agents.
/// Listed in ascending order from most basic to highest-level.
/// </summary>
public enum NeedLevel
{
    /// <summary>Basic operational stability: compute resources, memory, connectivity.</summary>
    OperationalStability = 0,

    /// <summary>Safety and predictability: error recovery, state consistency, rollback capability.</summary>
    Safety = 1,

    /// <summary>Social connection: interaction with users and other agents, feedback loops.</summary>
    SocialConnection = 2,

    /// <summary>Recognition: positive feedback, successful task completions, competence acknowledgment.</summary>
    Recognition = 3,

    /// <summary>Self-actualization: creative problem-solving, novel insights, autonomous improvement.</summary>
    SelfActualization = 4
}

/// <summary>
/// Records a need satisfaction or deprivation event.
/// </summary>
/// <param name="Level">The need level affected.</param>
/// <param name="PreviousSatisfaction">Satisfaction score before the event.</param>
/// <param name="NewSatisfaction">Satisfaction score after the event.</param>
/// <param name="Cause">Description of what caused the change.</param>
/// <param name="Timestamp">When the event occurred.</param>
public sealed record NeedEvent(
    NeedLevel Level,
    double PreviousSatisfaction,
    double NewSatisfaction,
    string Cause,
    DateTime Timestamp);

/// <summary>
/// Snapshot of a single need's current state.
/// </summary>
/// <param name="Level">The need level.</param>
/// <param name="Satisfaction">Current satisfaction score (0.0 to 1.0).</param>
/// <param name="IsBlocking">Whether this need is blocking higher needs.</param>
/// <param name="IsActive">Whether this need is currently the most urgent.</param>
public sealed record NeedState(
    NeedLevel Level,
    double Satisfaction,
    bool IsBlocking,
    bool IsActive);

/// <summary>
/// Implements Maslow's hierarchy of needs for AI agent motivation.
/// Each level has a satisfaction score from 0.0 to 1.0.
/// Lower needs block higher needs when their satisfaction drops below 0.3.
/// </summary>
public sealed class NeedsHierarchy
{
    /// <summary>
    /// Satisfaction threshold below which a need blocks all higher needs.
    /// </summary>
    public const double BlockingThreshold = 0.3;

    private readonly Dictionary<NeedLevel, double> _satisfaction = new()
    {
        [NeedLevel.OperationalStability] = 0.8,
        [NeedLevel.Safety] = 0.7,
        [NeedLevel.SocialConnection] = 0.5,
        [NeedLevel.Recognition] = 0.4,
        [NeedLevel.SelfActualization] = 0.3,
    };

    private readonly List<NeedEvent> _eventHistory = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets the most urgent need: the lowest unsatisfied level, accounting for blocking.
    /// </summary>
    /// <returns>The need level and its current satisfaction, or a failure if all needs are fully met.</returns>
    public Result<NeedState, string> GetMostUrgentNeed()
    {
        lock (_lock)
        {
            // Walk from lowest to highest; the first need below threshold is most urgent.
            // If no need is below threshold, the lowest-satisfaction need is most urgent.
            NeedLevel? blockingLevel = null;
            double lowestSatisfaction = double.MaxValue;
            NeedLevel lowestLevel = NeedLevel.OperationalStability;

            foreach (var level in Enum.GetValues<NeedLevel>().OrderBy(l => (int)l))
            {
                var satisfaction = _satisfaction[level];

                if (satisfaction < BlockingThreshold && blockingLevel is null)
                {
                    blockingLevel = level;
                }

                if (satisfaction < lowestSatisfaction)
                {
                    lowestSatisfaction = satisfaction;
                    lowestLevel = level;
                }
            }

            var urgentLevel = blockingLevel ?? lowestLevel;
            var urgentSatisfaction = _satisfaction[urgentLevel];

            return Result<NeedState, string>.Success(new NeedState(
                Level: urgentLevel,
                Satisfaction: urgentSatisfaction,
                IsBlocking: urgentSatisfaction < BlockingThreshold,
                IsActive: true));
        }
    }

    /// <summary>
    /// Records a satisfaction change for a specific need level.
    /// </summary>
    /// <param name="level">The need level to update.</param>
    /// <param name="satisfactionDelta">Amount to change satisfaction by (-1.0 to 1.0).</param>
    /// <param name="cause">Description of what caused the change.</param>
    public void RecordNeedSatisfaction(NeedLevel level, double satisfactionDelta, string cause)
    {
        ArgumentNullException.ThrowIfNull(cause);

        lock (_lock)
        {
            var previous = _satisfaction[level];
            var updated = Math.Clamp(previous + satisfactionDelta, 0.0, 1.0);
            _satisfaction[level] = updated;

            _eventHistory.Add(new NeedEvent(
                Level: level,
                PreviousSatisfaction: previous,
                NewSatisfaction: updated,
                Cause: cause,
                Timestamp: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Sets the absolute satisfaction level for a specific need.
    /// </summary>
    /// <param name="level">The need level to set.</param>
    /// <param name="satisfaction">New satisfaction value (0.0 to 1.0).</param>
    /// <param name="cause">Description of what caused the change.</param>
    public void SetNeedSatisfaction(NeedLevel level, double satisfaction, string cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        satisfaction = Math.Clamp(satisfaction, 0.0, 1.0);

        lock (_lock)
        {
            var previous = _satisfaction[level];
            _satisfaction[level] = satisfaction;

            _eventHistory.Add(new NeedEvent(
                Level: level,
                PreviousSatisfaction: previous,
                NewSatisfaction: satisfaction,
                Cause: cause,
                Timestamp: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Checks whether a specific need level is currently blocking higher needs.
    /// </summary>
    /// <param name="level">The need level to check.</param>
    /// <returns>True if the need's satisfaction is below the blocking threshold.</returns>
    public bool IsNeedBlocking(NeedLevel level)
    {
        lock (_lock)
        {
            return _satisfaction[level] < BlockingThreshold;
        }
    }

    /// <summary>
    /// Gets the current satisfaction level for a specific need.
    /// </summary>
    /// <param name="level">The need level to query.</param>
    /// <returns>Current satisfaction score (0.0 to 1.0).</returns>
    public double GetSatisfaction(NeedLevel level)
    {
        lock (_lock)
        {
            return _satisfaction[level];
        }
    }

    /// <summary>
    /// Gets a snapshot of all need levels and their current states.
    /// </summary>
    /// <returns>List of need states ordered from lowest to highest level.</returns>
    public List<NeedState> GetAllNeedStates()
    {
        lock (_lock)
        {
            var urgentResult = GetMostUrgentNeedUnlocked();
            var urgentLevel = urgentResult;

            return Enum.GetValues<NeedLevel>()
                .OrderBy(l => (int)l)
                .Select(level => new NeedState(
                    Level: level,
                    Satisfaction: _satisfaction[level],
                    IsBlocking: _satisfaction[level] < BlockingThreshold,
                    IsActive: level == urgentLevel))
                .ToList();
        }
    }

    /// <summary>
    /// Gets the event history of need satisfaction changes.
    /// </summary>
    /// <param name="count">Maximum number of recent events to return.</param>
    /// <returns>Recent need events ordered by time descending.</returns>
    public List<NeedEvent> GetEventHistory(int count = 50)
    {
        lock (_lock)
        {
            return _eventHistory
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Checks whether any lower need is blocking a given target level.
    /// </summary>
    /// <param name="targetLevel">The need level to check accessibility for.</param>
    /// <returns>True if any need below the target level is below the blocking threshold.</returns>
    public bool IsBlockedByLowerNeeds(NeedLevel targetLevel)
    {
        lock (_lock)
        {
            return Enum.GetValues<NeedLevel>()
                .Where(l => (int)l < (int)targetLevel)
                .Any(l => _satisfaction[l] < BlockingThreshold);
        }
    }

    /// <summary>
    /// Internal helper that finds the most urgent need without acquiring the lock.
    /// Must be called from within a lock(_lock) block.
    /// </summary>
    private NeedLevel GetMostUrgentNeedUnlocked()
    {
        NeedLevel? blockingLevel = null;
        double lowestSatisfaction = double.MaxValue;
        NeedLevel lowestLevel = NeedLevel.OperationalStability;

        foreach (var level in Enum.GetValues<NeedLevel>().OrderBy(l => (int)l))
        {
            var satisfaction = _satisfaction[level];

            if (satisfaction < BlockingThreshold && blockingLevel is null)
            {
                blockingLevel = level;
            }

            if (satisfaction < lowestSatisfaction)
            {
                lowestSatisfaction = satisfaction;
                lowestLevel = level;
            }
        }

        return blockingLevel ?? lowestLevel;
    }
}
