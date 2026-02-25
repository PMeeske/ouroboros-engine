// <copyright file="RetentionPolicy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Defines retention policies for pipeline branch snapshots.
/// Supports time-based and count-based retention strategies.
/// </summary>
public sealed record RetentionPolicy
{
    /// <summary>
    /// Gets or sets the maximum age of snapshots to retain.
    /// Snapshots older than this will be marked for deletion.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain per branch.
    /// When exceeded, oldest snapshots are marked for deletion.
    /// </summary>
    public int? MaxCount { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep at least one snapshot per branch regardless of age or count.
    /// </summary>
    public bool KeepAtLeastOne { get; init; } = true;

    /// <summary>
    /// Creates a retention policy based on maximum age.
    /// </summary>
    /// <param name="maxAge">Maximum age of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy ByAge(TimeSpan maxAge) => new() { MaxAge = maxAge };

    /// <summary>
    /// Creates a retention policy based on maximum count.
    /// </summary>
    /// <param name="maxCount">Maximum number of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy ByCount(int maxCount) => new() { MaxCount = maxCount };

    /// <summary>
    /// Creates a retention policy that combines age and count constraints.
    /// </summary>
    /// <param name="maxAge">Maximum age of snapshots to retain.</param>
    /// <param name="maxCount">Maximum number of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy Combined(TimeSpan maxAge, int maxCount) => new()
    {
        MaxAge = maxAge,
        MaxCount = maxCount
    };

    /// <summary>
    /// Creates a permissive retention policy that keeps everything.
    /// </summary>
    /// <returns>A new retention policy instance that retains all snapshots.</returns>
    public static RetentionPolicy KeepAll() => new();
}