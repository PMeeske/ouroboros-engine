// <copyright file="ExperienceBuffer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a single experience tuple: (current state, action taken, resulting next state).
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Current">The state before the action was taken.</param>
/// <param name="Action">The action that was applied.</param>
/// <param name="NextState">The state resulting from applying the action.</param>
public sealed record Experience(State Current, Action Action, State NextState);

/// <summary>
/// Circular buffer for storing experience tuples used in world model training.
/// Overwrites oldest experiences when capacity is reached.
/// Not thread-safe; designed for single-threaded training loops.
/// </summary>
public sealed class ExperienceBuffer
{
    private readonly Experience[] _buffer;
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperienceBuffer"/> class.
    /// </summary>
    /// <param name="capacity">Maximum number of experiences to store. Must be positive.</param>
    public ExperienceBuffer(int capacity = 10000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        _buffer = new Experience[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the current number of experiences in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets a value indicating whether the buffer is at full capacity.
    /// </summary>
    public bool IsFull => _count == _buffer.Length;

    /// <summary>
    /// Adds an experience to the buffer. If the buffer is full, overwrites the oldest entry.
    /// </summary>
    /// <param name="experience">The experience tuple to store.</param>
    public void Add(Experience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);

        _buffer[_head] = experience;
        _head = (_head + 1) % _buffer.Length;

        if (_count < _buffer.Length)
        {
            _count++;
        }
    }

    /// <summary>
    /// Samples a random subset of experiences from the buffer.
    /// </summary>
    /// <param name="count">Number of experiences to sample. Clamped to buffer size.</param>
    /// <param name="rng">Optional random number generator for deterministic sampling.</param>
    /// <returns>A list of sampled experiences.</returns>
    public List<Experience> Sample(int count, Random? rng = null)
    {
        rng ??= Random.Shared;
        int sampleCount = Math.Min(count, _count);

        var indices = new HashSet<int>();
        while (indices.Count < sampleCount)
        {
            indices.Add(rng.Next(_count));
        }

        var result = new List<Experience>(sampleCount);
        foreach (int idx in indices)
        {
            result.Add(_buffer[idx]);
        }

        return result;
    }
}