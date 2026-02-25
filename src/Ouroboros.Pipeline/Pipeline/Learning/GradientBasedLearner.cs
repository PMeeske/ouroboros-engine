namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Thread-safe gradient-based online learner implementing stochastic gradient descent
/// with momentum and adaptive learning rates.
/// </summary>
public sealed class GradientBasedLearner : IOnlineLearner
{
    private readonly object _lock = new();
    private readonly GradientLearnerConfig _config;
    private readonly ConcurrentDictionary<string, double> _parameters;
    private readonly ConcurrentDictionary<string, double> _momentumBuffer;
    private readonly ConcurrentDictionary<string, double> _squaredGradientBuffer;
    private readonly ConcurrentQueue<LearningUpdate> _pendingUpdates;
    private OnlineLearningMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradientBasedLearner"/> class.
    /// </summary>
    /// <param name="config">The learner configuration.</param>
    /// <param name="initialParameters">Optional initial parameter values.</param>
    public GradientBasedLearner(
        GradientLearnerConfig? config = null,
        IReadOnlyDictionary<string, double>? initialParameters = null)
    {
        _config = config ?? GradientLearnerConfig.Default;
        _parameters = new ConcurrentDictionary<string, double>(
            initialParameters ?? new Dictionary<string, double>());
        _momentumBuffer = new ConcurrentDictionary<string, double>();
        _squaredGradientBuffer = new ConcurrentDictionary<string, double>();
        _pendingUpdates = new ConcurrentQueue<LearningUpdate>();
        _metrics = OnlineLearningMetrics.Empty;
    }

    /// <inheritdoc />
    public OnlineLearningMetrics Metrics
    {
        get
        {
            lock (_lock)
            {
                return _metrics;
            }
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<LearningUpdate>, string> ProcessFeedback(Feedback feedback)
    {
        var validationResult = feedback.Validate();
        if (validationResult.IsFailure)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(validationResult.Error);
        }

        try
        {
            var updates = ComputeUpdates(feedback);

            lock (_lock)
            {
                _metrics = _metrics.WithNewScore(feedback.Score);

                foreach (var update in updates)
                {
                    _pendingUpdates.Enqueue(update);
                    _metrics = _metrics.WithGradient(Math.Abs(update.Gradient));
                }
            }

            // Auto-apply if batch size reached
            if (_pendingUpdates.Count >= _config.BatchAccumulationSize)
            {
                ApplyUpdates();
            }

            return Result<IReadOnlyList<LearningUpdate>, string>.Success(updates);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(
                $"Failed to process feedback: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<LearningUpdate>, string> ProcessBatch(IEnumerable<Feedback> batch)
    {
        var feedbackList = batch.ToList();
        if (feedbackList.Count == 0)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Success(
                Array.Empty<LearningUpdate>());
        }

        var allUpdates = new List<LearningUpdate>();
        var errors = new List<string>();

        foreach (var feedback in feedbackList)
        {
            var result = ProcessFeedback(feedback);
            if (result.IsSuccess)
            {
                allUpdates.AddRange(result.Value);
            }
            else
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0 && allUpdates.Count == 0)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(
                $"All feedback processing failed: {string.Join("; ", errors)}");
        }

        // Aggregate updates by parameter
        var aggregatedUpdates = AggregateUpdates(allUpdates);

        return Result<IReadOnlyList<LearningUpdate>, string>.Success(aggregatedUpdates);
    }

    /// <inheritdoc />
    public IReadOnlyList<LearningUpdate> GetPendingUpdates()
    {
        return _pendingUpdates.ToArray();
    }

    /// <inheritdoc />
    public Result<int, string> ApplyUpdates()
    {
        try
        {
            var updates = new List<LearningUpdate>();

            while (_pendingUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }

            if (updates.Count == 0)
            {
                return Result<int, string>.Success(0);
            }

            // Aggregate and apply
            var aggregated = AggregateUpdates(updates);
            var appliedCount = 0;

            foreach (var update in aggregated)
            {
                if (update.Confidence >= _config.MinConfidenceThreshold)
                {
                    // Apply momentum
                    var momentum = _momentumBuffer.GetOrAdd(update.ParameterName, 0.0);
                    var gradientWithMomentum = (_config.Momentum * momentum) +
                                               ((1 - _config.Momentum) * update.Gradient);
                    _momentumBuffer[update.ParameterName] = gradientWithMomentum;

                    // Compute effective learning rate (adaptive if configured)
                    var effectiveLr = ComputeEffectiveLearningRate(update.ParameterName, update.Gradient);

                    // Clip gradient
                    var clippedGradient = Math.Clamp(
                        gradientWithMomentum,
                        -_config.GradientClipThreshold,
                        _config.GradientClipThreshold);

                    // Apply update
                    var delta = -effectiveLr * clippedGradient;
                    _parameters.AddOrUpdate(
                        update.ParameterName,
                        update.OldValue + delta,
                        (_, current) => current + delta);

                    appliedCount++;
                }
            }

            return Result<int, string>.Success(appliedCount);
        }
        catch (Exception ex)
        {
            return Result<int, string>.Failure($"Failed to apply updates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Option<double> GetParameter(string parameterName)
    {
        return _parameters.TryGetValue(parameterName, out var value)
            ? Option<double>.Some(value)
            : Option<double>.None();
    }

    /// <inheritdoc />
    public void SetParameter(string parameterName, double value)
    {
        _parameters[parameterName] = value;
    }

    /// <summary>
    /// Gets all current parameter values as an immutable dictionary.
    /// </summary>
    /// <returns>The current parameters.</returns>
    public ImmutableDictionary<string, double> GetAllParameters()
    {
        return _parameters.ToImmutableDictionary();
    }

    /// <summary>
    /// Resets the learner state while preserving parameters.
    /// </summary>
    public void ResetState()
    {
        lock (_lock)
        {
            _momentumBuffer.Clear();
            _squaredGradientBuffer.Clear();
            while (_pendingUpdates.TryDequeue(out _))
            {
            }

            _metrics = OnlineLearningMetrics.Empty;
        }
    }

    private List<LearningUpdate> ComputeUpdates(Feedback feedback)
    {
        var updates = new List<LearningUpdate>();

        // For each parameter, compute gradient based on feedback
        foreach (var kvp in _parameters)
        {
            var parameterName = kvp.Key;
            var currentValue = kvp.Value;

            // Compute gradient as negative score (we want to maximize score)
            // Scale by feedback type importance
            var typeWeight = feedback.Type switch
            {
                FeedbackType.Explicit => 1.0,
                FeedbackType.Corrective => 1.5,
                FeedbackType.Comparative => 0.8,
                FeedbackType.Implicit => 0.5,
                _ => 1.0,
            };

            // Gradient is negative of score (to maximize score via gradient descent)
            var gradient = -feedback.Score * typeWeight;

            // Confidence based on feedback type and recency
            var confidence = ComputeConfidence(feedback);

            var update = LearningUpdate.FromGradient(
                parameterName,
                currentValue,
                gradient,
                _config.LearningRate,
                confidence);

            updates.Add(update);
        }

        // If no parameters exist, create a default "bias" parameter
        if (updates.Count == 0)
        {
            var defaultParam = "bias";
            var currentValue = _parameters.GetOrAdd(defaultParam, 0.0);
            var gradient = -feedback.Score;
            var confidence = ComputeConfidence(feedback);

            updates.Add(LearningUpdate.FromGradient(
                defaultParam,
                currentValue,
                gradient,
                _config.LearningRate,
                confidence));
        }

        return updates;
    }

    private double ComputeConfidence(Feedback feedback)
    {
        // Base confidence by feedback type
        var baseConfidence = feedback.Type switch
        {
            FeedbackType.Explicit => 1.0,
            FeedbackType.Corrective => 0.95,
            FeedbackType.Comparative => 0.8,
            FeedbackType.Implicit => 0.6,
            _ => 0.5,
        };

        // Decay confidence based on age (half-life of 1 hour)
        var age = DateTime.UtcNow - feedback.Timestamp;
        var timeFactor = Math.Exp(-age.TotalHours * 0.693); // ln(2) ≈ 0.693

        return baseConfidence * timeFactor;
    }

    private double ComputeEffectiveLearningRate(string parameterName, double gradient)
    {
        if (!_config.AdaptiveLearningRate)
        {
            return _config.LearningRate;
        }

        // RMSprop-style adaptive learning rate
        var epsilon = 1e-8;
        var decayRate = 0.99;

        var squaredGrad = _squaredGradientBuffer.GetOrAdd(parameterName, 0.0);
        var newSquaredGrad = (decayRate * squaredGrad) + ((1 - decayRate) * gradient * gradient);
        _squaredGradientBuffer[parameterName] = newSquaredGrad;

        return _config.LearningRate / (Math.Sqrt(newSquaredGrad) + epsilon);
    }

    private List<LearningUpdate> AggregateUpdates(List<LearningUpdate> updates)
    {
        var grouped = updates.GroupBy(u => u.ParameterName);
        var aggregated = new List<LearningUpdate>();

        foreach (var group in grouped)
        {
            var merged = group.Aggregate((a, b) => a.MergeWith(b));
            aggregated.Add(merged);
        }

        return aggregated;
    }
}