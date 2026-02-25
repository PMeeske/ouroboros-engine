using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Provides Kleisli arrow operations for online learning pipelines.
/// </summary>
public static class OnlineLearningArrow
{
    /// <summary>
    /// Creates a step that processes a single feedback item through the learner.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that transforms feedback into learning updates.</returns>
    public static Step<Feedback, Result<IReadOnlyList<LearningUpdate>, string>> ProcessFeedbackStep(
        IOnlineLearner learner)
    {
        return feedback => Task.FromResult(learner.ProcessFeedback(feedback));
    }

    /// <summary>
    /// Creates a step that processes a batch of feedback items.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that transforms a batch of feedback into learning updates.</returns>
    public static Step<IEnumerable<Feedback>, Result<IReadOnlyList<LearningUpdate>, string>> ProcessBatchStep(
        IOnlineLearner learner)
    {
        return batch => Task.FromResult(learner.ProcessBatch(batch));
    }

    /// <summary>
    /// Creates a step that applies pending updates and returns the count.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that applies updates and returns the count.</returns>
    public static Step<Unit, Result<int, string>> ApplyUpdatesStep(IOnlineLearner learner)
    {
        return _ => Task.FromResult(learner.ApplyUpdates());
    }

    /// <summary>
    /// Creates a step that retrieves the current performance metrics.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that returns the current metrics.</returns>
    public static Step<Unit, OnlineLearningMetrics> GetMetricsStep(IOnlineLearner learner)
    {
        return _ => Task.FromResult(learner.Metrics);
    }

    /// <summary>
    /// Creates a step that extracts explicit feedback from a scored output.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns>A step that creates explicit feedback from input/output/score tuples.</returns>
    public static Step<(string Input, string Output, double Score), Feedback> CreateExplicitFeedbackStep(
        string sourceId)
    {
        return tuple => Task.FromResult(
            Feedback.Explicit(sourceId, tuple.Input, tuple.Output, tuple.Score));
    }

    /// <summary>
    /// Creates a step that extracts corrective feedback from output corrections.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns>A step that creates corrective feedback from input/actual/preferred tuples.</returns>
    public static Step<(string Input, string ActualOutput, string PreferredOutput), Feedback> CreateCorrectiveFeedbackStep(
        string sourceId)
    {
        return tuple => Task.FromResult(
            Feedback.Corrective(sourceId, tuple.Input, tuple.ActualOutput, tuple.PreferredOutput));
    }

    /// <summary>
    /// Composes a full learning pipeline from feedback collection to update application.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <param name="sourceId">The source identifier for feedback.</param>
    /// <returns>A step that processes scored output and returns the applied update count.</returns>
    public static Step<(string Input, string Output, double Score), Result<int, string>> FullLearningPipeline(
        IOnlineLearner learner,
        string sourceId)
    {
        return async tuple =>
        {
            var feedback = Feedback.Explicit(sourceId, tuple.Input, tuple.Output, tuple.Score);
            var processResult = learner.ProcessFeedback(feedback);

            if (processResult.IsFailure)
            {
                return Result<int, string>.Failure(processResult.Error);
            }

            return learner.ApplyUpdates();
        };
    }

    /// <summary>
    /// Creates a step that filters feedback based on a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>A step that returns Some(feedback) if predicate passes, None otherwise.</returns>
    public static Step<Feedback, Option<Feedback>> FilterFeedbackStep(
        Func<Feedback, bool> predicate)
    {
        return feedback => Task.FromResult(
            predicate(feedback)
                ? Option<Feedback>.Some(feedback)
                : Option<Feedback>.None());
    }

    /// <summary>
    /// Creates a step that enriches feedback with additional tags.
    /// </summary>
    /// <param name="tagGenerator">Function that generates tags based on feedback.</param>
    /// <returns>A step that adds generated tags to the feedback.</returns>
    public static Step<Feedback, Feedback> EnrichFeedbackStep(
        Func<Feedback, IEnumerable<string>> tagGenerator)
    {
        return feedback =>
        {
            var newTags = tagGenerator(feedback);
            return Task.FromResult(feedback.WithTags(newTags.ToArray()));
        };
    }
}