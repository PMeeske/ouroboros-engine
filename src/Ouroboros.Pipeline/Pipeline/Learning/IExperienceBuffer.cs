namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Interface for experience buffer operations supporting replay-based learning.
/// </summary>
public interface IExperienceBuffer
{
    /// <summary>
    /// Gets the current number of experiences in the buffer.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Adds an experience to the buffer.
    /// If the buffer is at capacity, the oldest experience is evicted (FIFO).
    /// </summary>
    /// <param name="experience">The experience to add.</param>
    void Add(Experience experience);

    /// <summary>
    /// Samples a batch of experiences uniformly at random.
    /// </summary>
    /// <param name="batchSize">The number of experiences to sample.</param>
    /// <returns>A list of randomly sampled experiences.</returns>
    IReadOnlyList<Experience> Sample(int batchSize);

    /// <summary>
    /// Samples a batch of experiences using prioritized replay.
    /// Uses softmax distribution over priorities weighted by alpha.
    /// </summary>
    /// <param name="batchSize">The number of experiences to sample.</param>
    /// <param name="alpha">Temperature parameter controlling priority influence (0 = uniform, 1 = fully prioritized).</param>
    /// <returns>A list of priority-weighted sampled experiences.</returns>
    IReadOnlyList<Experience> SamplePrioritized(int batchSize, double alpha = 0.6);

    /// <summary>
    /// Clears all experiences from the buffer.
    /// </summary>
    void Clear();

    /// <summary>
    /// Updates the priority of an experience by ID.
    /// </summary>
    /// <param name="experienceId">The ID of the experience to update.</param>
    /// <param name="newPriority">The new priority value.</param>
    /// <returns>True if the experience was found and updated.</returns>
    bool UpdatePriority(Guid experienceId, double newPriority);
}