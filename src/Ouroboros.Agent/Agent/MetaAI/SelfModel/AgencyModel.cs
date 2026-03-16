// ==========================================================
// Agency Model Implementation
// Wegner (2002) Apparent Mental Causation for agent self-attribution
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Implements Wegner's (2002) Apparent Mental Causation model for
/// tracking and attributing agency to the agent's actions based on
/// prediction-outcome matching.
/// </summary>
public sealed class AgencyModel
{
    private const int MaxRecentActions = 200;

    private readonly ConcurrentDictionary<string, string> _predictions = new();
    private readonly ConcurrentDictionary<string, AgencyAttribution> _attributions = new();
    private readonly ConcurrentDictionary<string, AgencyType> _actionTypes = new();
    private readonly List<string> _actionOrder = new();
    private readonly object _lock = new();

    /// <summary>
    /// Records a voluntary action along with its predicted outcome.
    /// </summary>
    /// <param name="actionId">Unique identifier for the action.</param>
    /// <param name="predictedOutcome">What the agent expects to happen.</param>
    /// <returns>Success if recorded, failure if actionId already exists.</returns>
    public Result<bool, string> RecordVoluntaryAction(string actionId, string predictedOutcome)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return Result<bool, string>.Failure("Action ID must not be empty.");

        if (string.IsNullOrWhiteSpace(predictedOutcome))
            return Result<bool, string>.Failure("Predicted outcome must not be empty.");

        if (!_predictions.TryAdd(actionId, predictedOutcome))
            return Result<bool, string>.Failure($"Action '{actionId}' already recorded.");

        _actionTypes[actionId] = AgencyType.Voluntary;

        lock (_lock)
        {
            _actionOrder.Add(actionId);
            PruneOldActions();
        }

        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Records the actual outcome of a previously predicted action and computes
    /// the agency score based on prediction-outcome similarity.
    /// </summary>
    /// <param name="actionId">The action whose outcome is being recorded.</param>
    /// <param name="actualOutcome">What actually happened.</param>
    /// <returns>The agency attribution for the action.</returns>
    public Result<AgencyAttribution, string> RecordActionOutcome(
        string actionId,
        string actualOutcome)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return Result<AgencyAttribution, string>.Failure("Action ID must not be empty.");

        if (string.IsNullOrWhiteSpace(actualOutcome))
            return Result<AgencyAttribution, string>.Failure("Actual outcome must not be empty.");

        if (!_predictions.TryGetValue(actionId, out string? predicted))
            return Result<AgencyAttribution, string>.Failure(
                $"No prediction found for action '{actionId}'.");

        _actionTypes.TryGetValue(actionId, out AgencyType type);
        double agencyScore = JaccardSimilarity(predicted, actualOutcome);
        string narrative = BuildNarrative(type, actionId, agencyScore);

        var attribution = new AgencyAttribution(
            actionId,
            type,
            agencyScore,
            agencyScore,
            narrative);

        _attributions[actionId] = attribution;

        return Result<AgencyAttribution, string>.Success(attribution);
    }

    /// <summary>
    /// Returns the agency score for a specific action.
    /// </summary>
    /// <param name="actionId">The action to look up.</param>
    /// <returns>Agency score between 0.0 and 1.0.</returns>
    public Result<double, string> GetAgencyScore(string actionId)
    {
        if (_attributions.TryGetValue(actionId, out AgencyAttribution? attribution))
            return Result<double, string>.Success(attribution.AgencyScore);

        return Result<double, string>.Failure($"No attribution found for action '{actionId}'.");
    }

    /// <summary>
    /// Returns the average agency score across recent attributed actions.
    /// </summary>
    /// <returns>Overall agency score between 0.0 and 1.0.</returns>
    public double GetOverallAgencyScore()
    {
        List<AgencyAttribution> recent;
        lock (_lock)
        {
            IEnumerable<string> recentIds = _actionOrder.TakeLast(50);
            recent = recentIds
                .Where(id => _attributions.ContainsKey(id))
                .Select(id => _attributions[id])
                .ToList();
        }

        return recent.Count > 0
            ? recent.Average(a => a.AgencyScore)
            : 0.0;
    }

    /// <summary>
    /// Attributes agency type to an action based on contextual cues.
    /// </summary>
    /// <param name="actionId">The action to attribute.</param>
    /// <param name="wasUserRequested">Whether a user explicitly requested the action.</param>
    /// <param name="wasSystemTriggered">Whether an automated system triggered the action.</param>
    /// <param name="deliberationTimeMs">How long the agent deliberated before acting.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The determined agency type.</returns>
    public Task<Result<AgencyType, string>> AttributeAgencyAsync(
        string actionId,
        bool wasUserRequested,
        bool wasSystemTriggered,
        double deliberationTimeMs,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return Task.FromResult(
                Result<AgencyType, string>.Failure("Action ID must not be empty."));

        AgencyType type;

        if (wasSystemTriggered)
        {
            type = AgencyType.Triggered;
        }
        else if (wasUserRequested && deliberationTimeMs < 50)
        {
            type = AgencyType.Reflexive;
        }
        else if (wasUserRequested)
        {
            type = AgencyType.Reactive;
        }
        else
        {
            type = AgencyType.Voluntary;
        }

        _actionTypes[actionId] = type;

        return Task.FromResult(Result<AgencyType, string>.Success(type));
    }

    /// <summary>
    /// Returns all attributions, ordered by most recent first.
    /// </summary>
    /// <returns>List of agency attributions.</returns>
    public IReadOnlyList<AgencyAttribution> GetAllAttributions()
    {
        lock (_lock)
        {
            return _actionOrder
                .Where(id => _attributions.ContainsKey(id))
                .Select(id => _attributions[id])
                .Reverse()
                .ToList()
                .AsReadOnly();
        }
    }

    private static double JaccardSimilarity(string a, string b)
    {
        HashSet<string> setA = new(
            a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> setB = new(
            b.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        if (setA.Count == 0 && setB.Count == 0)
            return 1.0;

        int intersection = setA.Intersect(setB).Count();
        int union = setA.Union(setB).Count();

        return union > 0 ? intersection / (double)union : 0.0;
    }

    private static string BuildNarrative(AgencyType type, string actionId, double score)
    {
        string confidence = score > 0.7 ? "as expected" : "with some unexpected results";

        return type switch
        {
            AgencyType.Voluntary =>
                $"I chose to perform '{actionId}' {confidence}.",
            AgencyType.Reactive =>
                $"I responded by performing '{actionId}' {confidence}.",
            AgencyType.Triggered =>
                $"The system triggered '{actionId}' {confidence}.",
            AgencyType.Reflexive =>
                $"I reflexively performed '{actionId}' {confidence}.",
            _ =>
                $"Action '{actionId}' was performed {confidence}."
        };
    }

    private void PruneOldActions()
    {
        while (_actionOrder.Count > MaxRecentActions)
        {
            string oldest = _actionOrder[0];
            _actionOrder.RemoveAt(0);
            _predictions.TryRemove(oldest, out _);
            _attributions.TryRemove(oldest, out _);
            _actionTypes.TryRemove(oldest, out _);
        }
    }
}
