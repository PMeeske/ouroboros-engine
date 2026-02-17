using System.Runtime.CompilerServices;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// An agent that continuously learns and adapts its behavior based on feedback.
/// Uses exponential moving average for performance tracking and maintains adaptation history for rollback.
/// Thread-safe implementation suitable for concurrent access.
/// </summary>
public sealed class ContinuouslyLearningAgent : IAdaptiveAgent
{
    private readonly object _lock = new();
    private readonly AdaptiveAgentConfig _config;
    private readonly ExperienceBuffer _experienceBuffer;

    private AgentPerformance _performance;
    private AdaptiveState _state;
    private ImmutableList<AdaptationEvent> _adaptationHistory;

    // Exponential moving average state
    private double _emaQuality;
    private double _emaSuccessRate;
    private bool _emaInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuouslyLearningAgent"/> class.
    /// </summary>
    /// <param name="agentId">Optional agent ID (auto-generated if not provided).</param>
    /// <param name="config">Optional configuration (defaults used if not provided).</param>
    /// <param name="bufferCapacity">Capacity of the internal experience buffer.</param>
    public ContinuouslyLearningAgent(
        Guid? agentId = null,
        AdaptiveAgentConfig? config = null,
        int bufferCapacity = 10000)
    {
        AgentId = agentId ?? Guid.NewGuid();
        _config = config ?? AdaptiveAgentConfig.Default;
        _experienceBuffer = new ExperienceBuffer(bufferCapacity);
        _performance = AgentPerformance.Initial(AgentId);
        _state = AdaptiveState.Initial();
        _adaptationHistory = ImmutableList<AdaptationEvent>.Empty;
        _emaQuality = 0.0;
        _emaSuccessRate = 0.0;
        _emaInitialized = false;
    }

    /// <inheritdoc/>
    public Guid AgentId { get; }

    /// <inheritdoc/>
    public Result<AgentPerformance, string> RecordInteraction(string input, string output, double quality)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<AgentPerformance, string>.Failure("Input cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return Result<AgentPerformance, string>.Failure("Output cannot be empty");
        }

        var clampedQuality = Math.Clamp(quality, -1.0, 1.0);
        var isSuccess = clampedQuality > 0;

        lock (_lock)
        {
            // Add to experience buffer for potential replay
            var experience = Experience.Create(
                state: input,
                action: output,
                reward: clampedQuality,
                nextState: string.Empty,
                priority: Math.Abs(clampedQuality) + 0.1);
            _experienceBuffer.Add(experience);

            // Update EMA
            UpdateExponentialMovingAverage(clampedQuality, isSuccess);

            // Update performance record
            var newTotalInteractions = _performance.TotalInteractions + 1;

            _performance = _performance with
            {
                TotalInteractions = newTotalInteractions,
                SuccessRate = _emaSuccessRate,
                AverageResponseQuality = _emaQuality,
                LastUpdated = DateTime.UtcNow,
            };

            // Record to learning curve periodically
            if (newTotalInteractions % 10 == 0)
            {
                _performance = _performance.WithLearningCurveEntry(_emaQuality);
            }

            return Result<AgentPerformance, string>.Success(_performance);
        }
    }

    /// <inheritdoc/>
    public bool ShouldAdapt()
    {
        lock (_lock)
        {
            // Don't adapt too early
            if (_performance.TotalInteractions < _config.MinInteractionsBeforeAdaptation)
            {
                return false;
            }

            // Check for performance decline
            var performanceDecline = _state.BaselinePerformance - _emaQuality;
            if (performanceDecline > _config.AdaptationThreshold)
            {
                return true;
            }

            // Check for stagnation
            if (_performance.IsStagnating(_config.StagnationWindowSize))
            {
                return true;
            }

            // Check for negative trend
            var trend = _performance.CalculateTrend(_config.StagnationWindowSize);
            if (trend < -_config.AdaptationThreshold / 10)
            {
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public Result<AdaptationEvent, string> Adapt()
    {
        lock (_lock)
        {
            if (_performance.TotalInteractions < _config.MinInteractionsBeforeAdaptation)
            {
                return Result<AdaptationEvent, string>.Failure(
                    $"Insufficient interactions for adaptation: {_performance.TotalInteractions} < {_config.MinInteractionsBeforeAdaptation}");
            }

            // Determine adaptation type based on current state
            var (eventType, description, newStrategy) = DetermineAdaptation();

            // Create adaptation event
            var adaptationEvent = AdaptationEvent.Create(
                AgentId,
                eventType,
                description,
                _performance);

            // Apply the adaptation
            var previousStrategy = _state.CurrentStrategy;
            _state = _state with
            {
                CurrentStrategy = newStrategy,
                BaselinePerformance = _emaQuality,
                PreviousStrategies = _state.PreviousStrategies.Push(previousStrategy),
            };

            // Add to history with size limit
            _adaptationHistory = _adaptationHistory.Add(adaptationEvent);
            if (_adaptationHistory.Count > _config.MaxAdaptationHistory)
            {
                _adaptationHistory = _adaptationHistory.RemoveAt(0);
            }

            return Result<AdaptationEvent, string>.Success(adaptationEvent);
        }
    }

    /// <inheritdoc/>
    public AgentPerformance GetPerformance()
    {
        lock (_lock)
        {
            return _performance;
        }
    }

    /// <inheritdoc/>
    public ImmutableList<AdaptationEvent> GetAdaptationHistory()
    {
        lock (_lock)
        {
            return _adaptationHistory;
        }
    }

    /// <inheritdoc/>
    public Result<AdaptationEvent, string> Rollback(Guid adaptationId)
    {
        lock (_lock)
        {
            // Find the adaptation to rollback
            var adaptationIndex = _adaptationHistory.FindIndex(e => e.Id == adaptationId);
            if (adaptationIndex < 0)
            {
                return Result<AdaptationEvent, string>.Failure($"Adaptation not found: {adaptationId}");
            }

            var targetAdaptation = _adaptationHistory[adaptationIndex];

            // Can only rollback if we have previous strategies
            if (_state.PreviousStrategies.IsEmpty)
            {
                return Result<AdaptationEvent, string>.Failure("No previous strategies available for rollback");
            }

            // Pop the previous strategy
            var previousStrategy = _state.PreviousStrategies.Peek();
            var remainingStack = _state.PreviousStrategies.Pop();

            // Create rollback event
            var rollbackEvent = AdaptationEvent.Create(
                AgentId,
                AdaptationEventType.Rollback,
                $"Rolled back adaptation: {targetAdaptation.Description}",
                _performance);

            // Apply rollback
            _state = _state with
            {
                CurrentStrategy = previousStrategy,
                BaselinePerformance = targetAdaptation.BeforeMetrics.AverageResponseQuality,
                PreviousStrategies = remainingStack,
            };

            // Update the original adaptation with after-metrics
            _adaptationHistory = _adaptationHistory.SetItem(
                adaptationIndex,
                targetAdaptation.WithAfterMetrics(_performance));

            // Add rollback event
            _adaptationHistory = _adaptationHistory.Add(rollbackEvent);
            if (_adaptationHistory.Count > _config.MaxAdaptationHistory)
            {
                _adaptationHistory = _adaptationHistory.RemoveAt(0);
            }

            return Result<AdaptationEvent, string>.Success(rollbackEvent);
        }
    }

    /// <summary>
    /// Gets the current learning strategy being used.
    /// </summary>
    /// <returns>The current LearningStrategy.</returns>
    public LearningStrategy GetCurrentStrategy()
    {
        lock (_lock)
        {
            return _state.CurrentStrategy;
        }
    }

    /// <summary>
    /// Gets the number of experiences in the buffer.
    /// </summary>
    /// <returns>The count of stored experiences.</returns>
    public int GetExperienceCount() => _experienceBuffer.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateExponentialMovingAverage(double quality, bool isSuccess)
    {
        var alpha = _config.EmaAlpha;
        var successValue = isSuccess ? 1.0 : 0.0;

        if (!_emaInitialized)
        {
            _emaQuality = quality;
            _emaSuccessRate = successValue;
            _emaInitialized = true;
        }
        else
        {
            _emaQuality = (alpha * quality) + ((1 - alpha) * _emaQuality);
            _emaSuccessRate = (alpha * successValue) + ((1 - alpha) * _emaSuccessRate);
        }
    }

    private (AdaptationEventType Type, string Description, LearningStrategy NewStrategy) DetermineAdaptation()
    {
        var currentStrategy = _state.CurrentStrategy;
        var trend = _performance.CalculateTrend(_config.StagnationWindowSize);
        var isStagnating = _performance.IsStagnating(_config.StagnationWindowSize);

        // If stagnating with low exploration, increase exploration
        if (isStagnating && currentStrategy.ExplorationRate < 0.3)
        {
            var newStrategy = currentStrategy
                .WithExplorationRate(Math.Min(currentStrategy.ExplorationRate * 2, 0.5))
                .WithLearningRate(Math.Min(currentStrategy.LearningRate * 1.5, 0.1));
            return (
                AdaptationEventType.StrategyChange,
                $"Increased exploration due to stagnation (ε: {currentStrategy.ExplorationRate:F3} → {newStrategy.ExplorationRate:F3})",
                newStrategy);
        }

        // If performance is declining, adjust learning rate
        if (trend < -0.01)
        {
            var newStrategy = currentStrategy
                .WithLearningRate(currentStrategy.LearningRate * 0.5);
            return (
                AdaptationEventType.ParameterTune,
                $"Reduced learning rate due to declining performance (lr: {currentStrategy.LearningRate:F5} → {newStrategy.LearningRate:F5})",
                newStrategy);
        }

        // If performing well, reduce exploration for exploitation
        if (_emaQuality > 0.7 && currentStrategy.ExplorationRate > 0.1)
        {
            var newStrategy = currentStrategy
                .WithExplorationRate(currentStrategy.ExplorationRate * 0.7);
            return (
                AdaptationEventType.ParameterTune,
                $"Reduced exploration for exploitation phase (ε: {currentStrategy.ExplorationRate:F3} → {newStrategy.ExplorationRate:F3})",
                newStrategy);
        }

        // Default: small parameter adjustment
        var defaultNewStrategy = currentStrategy
            .WithLearningRate(currentStrategy.LearningRate * 0.9);
        return (
            AdaptationEventType.ParameterTune,
            $"Minor learning rate adjustment (lr: {currentStrategy.LearningRate:F5} → {defaultNewStrategy.LearningRate:F5})",
            defaultNewStrategy);
    }
}