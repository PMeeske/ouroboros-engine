using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Provides Kleisli arrow factories for experience replay pipeline integration.
/// </summary>
public static class ExperienceReplayArrows
{
    /// <summary>
    /// Creates an arrow that adds an experience to the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to add to.</param>
    /// <returns>A step that adds the experience and returns Unit on success.</returns>
    public static Step<Experience, Result<Unit, string>> AddExperienceArrow(IExperienceBuffer buffer)
        => experience => Task.FromResult(AddExperience(buffer, experience));

    /// <summary>
    /// Creates an arrow that samples experiences uniformly from the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to sample from.</param>
    /// <returns>A step that samples the specified batch size of experiences.</returns>
    public static Step<int, Result<IReadOnlyList<Experience>, string>> SampleExperiencesArrow(IExperienceBuffer buffer)
        => batchSize => Task.FromResult(SampleExperiences(buffer, batchSize));

    /// <summary>
    /// Creates an arrow that samples experiences using prioritized replay.
    /// </summary>
    /// <param name="buffer">The experience buffer to sample from.</param>
    /// <param name="alpha">Temperature parameter for priority weighting.</param>
    /// <returns>A step that samples the specified batch size with priority weighting.</returns>
    public static Step<int, Result<IReadOnlyList<Experience>, string>> SamplePrioritizedArrow(
        IExperienceBuffer buffer,
        double alpha = 0.6)
        => batchSize => Task.FromResult(SamplePrioritized(buffer, batchSize, alpha));

    /// <summary>
    /// Creates an arrow that updates the priority of an experience.
    /// </summary>
    /// <param name="buffer">The experience buffer to update.</param>
    /// <returns>A step that updates priority and returns Unit on success.</returns>
    public static Step<(Guid Id, double NewPriority), Result<Unit, string>> UpdatePriorityArrow(IExperienceBuffer buffer)
        => input => Task.FromResult(UpdatePriority(buffer, input.Id, input.NewPriority));

    /// <summary>
    /// Creates an arrow that records an experience from state-action-reward-next_state tuple.
    /// </summary>
    /// <param name="buffer">The experience buffer to add to.</param>
    /// <param name="priority">Initial priority for the experience.</param>
    /// <returns>A step that creates and stores an experience from the input tuple.</returns>
    public static Step<(string State, string Action, double Reward, string NextState), Result<Experience, string>> RecordExperienceArrow(
        IExperienceBuffer buffer,
        double priority = 1.0)
        => input => Task.FromResult(RecordExperience(buffer, input.State, input.Action, input.Reward, input.NextState, priority));

    /// <summary>
    /// Creates an arrow that clears all experiences from the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to clear.</param>
    /// <returns>A step that clears the buffer and returns Unit.</returns>
    public static Step<Unit, Result<Unit, string>> ClearBufferArrow(IExperienceBuffer buffer)
        => _ =>
        {
            buffer.Clear();
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        };

    /// <summary>
    /// Creates an arrow that returns the current buffer statistics.
    /// </summary>
    /// <param name="buffer">The experience buffer to query.</param>
    /// <returns>A step that returns buffer count and capacity.</returns>
    public static Step<Unit, (int Count, int Capacity)> GetBufferStatsArrow(IExperienceBuffer buffer)
        => _ => Task.FromResult((buffer.Count, buffer.Capacity));

    private static Result<Unit, string> AddExperience(IExperienceBuffer buffer, Experience experience)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(experience);
            buffer.Add(experience);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add experience: {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<Experience>, string> SampleExperiences(IExperienceBuffer buffer, int batchSize)
    {
        try
        {
            if (batchSize <= 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Batch size must be positive.");
            }

            if (buffer.Count == 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Buffer is empty.");
            }

            var samples = buffer.Sample(batchSize);
            return Result<IReadOnlyList<Experience>, string>.Success(samples);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Experience>, string>.Failure($"Failed to sample experiences: {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<Experience>, string> SamplePrioritized(
        IExperienceBuffer buffer,
        int batchSize,
        double alpha)
    {
        try
        {
            if (batchSize <= 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Batch size must be positive.");
            }

            if (buffer.Count == 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Buffer is empty.");
            }

            var samples = buffer.SamplePrioritized(batchSize, alpha);
            return Result<IReadOnlyList<Experience>, string>.Success(samples);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Experience>, string>.Failure($"Failed to sample prioritized experiences: {ex.Message}");
        }
    }

    private static Result<Unit, string> UpdatePriority(IExperienceBuffer buffer, Guid experienceId, double newPriority)
    {
        try
        {
            if (buffer.UpdatePriority(experienceId, newPriority))
            {
                return Result<Unit, string>.Success(Unit.Value);
            }

            return Result<Unit, string>.Failure($"Experience with ID {experienceId} not found.");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to update priority: {ex.Message}");
        }
    }

    private static Result<Experience, string> RecordExperience(
        IExperienceBuffer buffer,
        string state,
        string action,
        double reward,
        string nextState,
        double priority)
    {
        try
        {
            var experience = Experience.Create(state, action, reward, nextState, priority);
            buffer.Add(experience);
            return Result<Experience, string>.Success(experience);
        }
        catch (Exception ex)
        {
            return Result<Experience, string>.Failure($"Failed to record experience: {ex.Message}");
        }
    }
}