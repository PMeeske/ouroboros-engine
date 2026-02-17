namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Extension methods for integrating online learning with other pipeline components.
/// </summary>
public static class OnlineLearningExtensions
{
    /// <summary>
    /// Converts feedback to an experience for replay buffer storage.
    /// </summary>
    /// <param name="feedback">The feedback to convert.</param>
    /// <param name="nextContext">The resulting context after the feedback.</param>
    /// <returns>An Experience record for the replay buffer.</returns>
    public static Experience ToExperience(this Feedback feedback, string nextContext)
    {
        return Experience.Create(
            state: feedback.InputContext,
            action: feedback.Output,
            reward: feedback.Score,
            nextState: nextContext,
            priority: feedback.Type switch
            {
                FeedbackType.Explicit => 1.0,
                FeedbackType.Corrective => 1.5,
                FeedbackType.Comparative => 0.8,
                FeedbackType.Implicit => 0.5,
                _ => 1.0,
            },
            metadata: ImmutableDictionary<string, object>.Empty
                .Add("feedbackId", feedback.Id)
                .Add("sourceId", feedback.SourceId)
                .Add("feedbackType", feedback.Type.ToString()));
    }

    /// <summary>
    /// Converts an experience back to feedback for reprocessing.
    /// </summary>
    /// <param name="experience">The experience to convert.</param>
    /// <returns>A Feedback record derived from the experience.</returns>
    public static Feedback ToFeedback(this Experience experience)
    {
        var sourceId = experience.Metadata.TryGetValue("sourceId", out var sid)
            ? sid?.ToString() ?? "unknown"
            : "unknown";

        var feedbackType = experience.Metadata.TryGetValue("feedbackType", out var ft)
            ? Enum.TryParse<FeedbackType>(ft?.ToString(), out var parsed) ? parsed : FeedbackType.Implicit
            : FeedbackType.Implicit;

        return new Feedback(
            Id: experience.Metadata.TryGetValue("feedbackId", out var fid) && fid is Guid guid
                ? guid
                : Guid.NewGuid(),
            SourceId: sourceId,
            InputContext: experience.State,
            Output: experience.Action,
            Score: experience.Reward,
            Type: feedbackType,
            Timestamp: experience.Timestamp,
            Tags: ImmutableList<string>.Empty);
    }

    /// <summary>
    /// Creates a learning-aware step that collects feedback for each execution.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="step">The step to wrap.</param>
    /// <param name="learner">The learner to send feedback to.</param>
    /// <param name="sourceId">The source identifier.</param>
    /// <param name="scoreFunc">Function to compute score from input and output.</param>
    /// <returns>A step that executes the original step and records feedback.</returns>
    public static Step<TInput, TOutput> WithLearning<TInput, TOutput>(
        this Step<TInput, TOutput> step,
        IOnlineLearner learner,
        string sourceId,
        Func<TInput, TOutput, double> scoreFunc)
        where TInput : notnull
        where TOutput : notnull
    {
        return async input =>
        {
            var output = await step(input);

            var score = scoreFunc(input, output);
            var feedback = Feedback.Explicit(
                sourceId,
                input.ToString()!,
                output.ToString()!,
                score);

            learner.ProcessFeedback(feedback);

            return output;
        };
    }
}