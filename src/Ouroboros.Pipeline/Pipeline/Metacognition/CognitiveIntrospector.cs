using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Thread-safe implementation of IIntrospector with rolling history and pattern detection.
/// </summary>
public sealed partial class CognitiveIntrospector : IIntrospector
{
    private readonly object _lock = new();
    private readonly int _maxHistorySize;
    private readonly List<InternalState> _history;
    private InternalState _currentState;

    private const double HighCognitiveLoadThreshold = 0.8;
    private const double LowCognitiveLoadThreshold = 0.2;
    private const double AttentionDriftThreshold = 0.3;
    private const double ValenceExtremeThreshold = 0.7;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveIntrospector"/> class.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of states to retain in history.</param>
    public CognitiveIntrospector(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize > 0 ? maxHistorySize : 100;
        _history = new List<InternalState>();
        _currentState = InternalState.Initial();
    }

    /// <inheritdoc/>
    public Result<InternalState, string> CaptureState()
    {
        lock (_lock)
        {
            try
            {
                var snapshot = _currentState.Snapshot();
                _history.Add(snapshot);

                // Maintain rolling window
                while (_history.Count > _maxHistorySize)
                {
                    _history.RemoveAt(0);
                }

                return Result<InternalState, string>.Success(snapshot);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return Result<InternalState, string>.Failure($"Failed to capture state: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public Result<IntrospectionReport, string> Analyze(InternalState state)
    {
        if (state is null)
        {
            return Result<IntrospectionReport, string>.Failure("Cannot analyze null state.");
        }

        try
        {
            var report = IntrospectionReport.Empty(state);

            // Analyze cognitive load
            report = AnalyzeCognitiveLoad(report, state);

            // Analyze emotional valence
            report = AnalyzeValence(report, state);

            // Analyze attention distribution
            report = AnalyzeAttention(report, state);

            // Analyze working memory
            report = AnalyzeWorkingMemory(report, state);

            // Analyze goals
            report = AnalyzeGoals(report, state);

            // Analyze processing mode
            report = AnalyzeMode(report, state);

            // Calculate self-assessment score
            var score = CalculateSelfAssessmentScore(state, report);
            report = report with { SelfAssessmentScore = score, GeneratedAt = DateTime.UtcNow };

            return Result<IntrospectionReport, string>.Success(report);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<IntrospectionReport, string>.Failure($"Analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<StateComparison, string> CompareStates(InternalState before, InternalState after)
    {
        if (before is null || after is null)
        {
            return Result<StateComparison, string>.Failure("Cannot compare null states.");
        }

        try
        {
            var comparison = StateComparison.Create(before, after);
            return Result<StateComparison, string>.Success(comparison);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<StateComparison, string>.Failure($"Comparison failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<ImmutableList<string>, string> IdentifyPatterns(IEnumerable<InternalState> history)
    {
        if (history is null)
        {
            return Result<ImmutableList<string>, string>.Failure("Cannot identify patterns in null history.");
        }

        try
        {
            var states = history.ToList();
            if (states.Count < 2)
            {
                return Result<ImmutableList<string>, string>.Success(
                    ImmutableList.Create("Insufficient history for pattern detection."));
            }

            var patterns = new List<string>();

            // Pattern: Cognitive load trends
            var loadTrend = DetectTrend(states.Select(s => s.CognitiveLoad).ToList());
            if (loadTrend != TrendDirection.Stable)
            {
                patterns.Add($"Cognitive load shows {loadTrend.ToString().ToLowerInvariant()} trend over time.");
            }

            // Pattern: Valence oscillation
            var valenceOscillation = DetectOscillation(states.Select(s => s.EmotionalValence).ToList());
            if (valenceOscillation)
            {
                patterns.Add("Emotional valence shows oscillating pattern, indicating processing instability.");
            }

            // Pattern: Mode switching frequency
            var modeChanges = CountModeChanges(states);
            if (modeChanges > states.Count / 3)
            {
                patterns.Add($"Frequent processing mode changes detected ({modeChanges} transitions), suggesting task-switching behavior.");
            }

            // Pattern: Goal completion rate
            var goalPattern = AnalyzeGoalPatterns(states);
            if (!string.IsNullOrEmpty(goalPattern))
            {
                patterns.Add(goalPattern);
            }

            // Pattern: Focus stability
            var focusChanges = CountFocusChanges(states);
            if (focusChanges > states.Count / 2)
            {
                patterns.Add("Attention focus is unstable, changing frequently between states.");
            }

            // Pattern: Working memory saturation
            var avgMemoryItems = states.Average(s => s.WorkingMemoryItems.Count);
            if (avgMemoryItems > 7)
            {
                patterns.Add($"Working memory consistently near capacity (avg: {avgMemoryItems:F1} items).");
            }

            // Pattern: Chronic high load
            var highLoadStates = states.Count(s => s.CognitiveLoad > HighCognitiveLoadThreshold);
            if (highLoadStates > states.Count * 0.6)
            {
                patterns.Add("Chronic high cognitive load detected across majority of states.");
            }

            if (patterns.Count == 0)
            {
                patterns.Add("No significant patterns detected in state history.");
            }

            return Result<ImmutableList<string>, string>.Success(patterns.ToImmutableList());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<ImmutableList<string>, string>.Failure($"Pattern identification failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<ImmutableList<InternalState>, string> GetStateHistory()
    {
        lock (_lock)
        {
            return Result<ImmutableList<InternalState>, string>.Success(_history.ToImmutableList());
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetCurrentFocus(string focus)
    {
        if (string.IsNullOrWhiteSpace(focus))
        {
            return Result<Unit, string>.Failure("Focus cannot be null or empty.");
        }

        lock (_lock)
        {
            _currentState = _currentState.WithFocus(focus);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> AddGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<Unit, string>.Failure("Goal cannot be null or empty.");
        }

        lock (_lock)
        {
            if (_currentState.ActiveGoals.Contains(goal))
            {
                return Result<Unit, string>.Failure($"Goal '{goal}' already exists.");
            }

            _currentState = _currentState.WithGoal(goal);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> RemoveGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<Unit, string>.Failure("Goal cannot be null or empty.");
        }

        lock (_lock)
        {
            if (!_currentState.ActiveGoals.Contains(goal))
            {
                return Result<Unit, string>.Failure($"Goal '{goal}' not found.");
            }

            _currentState = _currentState.WithoutGoal(goal);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetCognitiveLoad(double load)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithCognitiveLoad(load);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetValence(double valence)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithValence(valence);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetMode(ProcessingMode mode)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithMode(mode);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

}