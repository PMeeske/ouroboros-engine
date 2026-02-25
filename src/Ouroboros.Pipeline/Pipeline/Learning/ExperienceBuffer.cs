namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Thread-safe experience buffer with fixed capacity and FIFO eviction.
/// Supports both uniform and prioritized experience replay sampling.
/// </summary>
public sealed class ExperienceBuffer : IExperienceBuffer
{
    private readonly object _lock = new();
    private readonly LinkedList<Experience> _experiences = new();
    private readonly Dictionary<Guid, LinkedListNode<Experience>> _index = new();
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperienceBuffer"/> class.
    /// </summary>
    /// <param name="capacity">Maximum number of experiences to store.</param>
    /// <param name="seed">Optional random seed for reproducible sampling.</param>
    public ExperienceBuffer(int capacity = 10000, int? seed = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        Capacity = capacity;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _experiences.Count;
            }
        }
    }

    /// <inheritdoc/>
    public int Capacity { get; }

    /// <inheritdoc/>
    public void Add(Experience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);

        lock (_lock)
        {
            // FIFO eviction when at capacity
            while (_experiences.Count >= Capacity)
            {
                var oldest = _experiences.First;
                if (oldest is not null)
                {
                    _index.Remove(oldest.Value.Id);
                    _experiences.RemoveFirst();
                }
            }

            // Add new experience at the end
            var node = _experiences.AddLast(experience);
            _index[experience.Id] = node;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Experience> Sample(int batchSize)
    {
        if (batchSize <= 0)
        {
            return Array.Empty<Experience>();
        }

        lock (_lock)
        {
            if (_experiences.Count == 0)
            {
                return Array.Empty<Experience>();
            }

            var effectiveBatchSize = Math.Min(batchSize, _experiences.Count);
            var experiences = _experiences.ToArray();
            var sampled = new List<Experience>(effectiveBatchSize);
            var usedIndices = new HashSet<int>();

            while (sampled.Count < effectiveBatchSize)
            {
                var index = _random.Next(experiences.Length);
                if (usedIndices.Add(index))
                {
                    sampled.Add(experiences[index]);
                }
            }

            return sampled;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Experience> SamplePrioritized(int batchSize, double alpha = 0.6)
    {
        if (batchSize <= 0)
        {
            return Array.Empty<Experience>();
        }

        lock (_lock)
        {
            if (_experiences.Count == 0)
            {
                return Array.Empty<Experience>();
            }

            var effectiveBatchSize = Math.Min(batchSize, _experiences.Count);
            var experiences = _experiences.ToArray();

            // Compute softmax probabilities based on priorities
            var probabilities = ComputePrioritizedProbabilities(experiences, alpha);

            var sampled = new List<Experience>(effectiveBatchSize);
            var usedIndices = new HashSet<int>();

            while (sampled.Count < effectiveBatchSize)
            {
                var index = SampleFromDistribution(probabilities);
                if (usedIndices.Add(index))
                {
                    sampled.Add(experiences[index]);
                }
            }

            return sampled;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            _experiences.Clear();
            _index.Clear();
        }
    }

    /// <inheritdoc/>
    public bool UpdatePriority(Guid experienceId, double newPriority)
    {
        lock (_lock)
        {
            if (!_index.TryGetValue(experienceId, out var node))
            {
                return false;
            }

            var updated = node.Value with { Priority = newPriority };
            node.Value = updated;
            return true;
        }
    }

    /// <summary>
    /// Gets all experiences as a read-only list (primarily for testing/debugging).
    /// </summary>
    /// <returns>A list of all experiences in the buffer.</returns>
    public IReadOnlyList<Experience> GetAll()
    {
        lock (_lock)
        {
            return _experiences.ToList();
        }
    }

    /// <summary>
    /// Computes prioritized sampling probabilities using softmax with temperature alpha.
    /// </summary>
    private static double[] ComputePrioritizedProbabilities(Experience[] experiences, double alpha)
    {
        var priorities = experiences.Select(e => Math.Pow(e.Priority, alpha)).ToArray();
        var sum = priorities.Sum();

        // Avoid division by zero
        if (sum <= 0)
        {
            var uniformProb = 1.0 / experiences.Length;
            return Enumerable.Repeat(uniformProb, experiences.Length).ToArray();
        }

        return priorities.Select(p => p / sum).ToArray();
    }

    /// <summary>
    /// Samples an index from a discrete probability distribution.
    /// </summary>
    private int SampleFromDistribution(double[] probabilities)
    {
        var value = _random.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < probabilities.Length; i++)
        {
            cumulative += probabilities[i];
            if (value <= cumulative)
            {
                return i;
            }
        }

        // Fallback for floating-point edge cases
        return probabilities.Length - 1;
    }
}