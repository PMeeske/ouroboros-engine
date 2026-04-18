// <copyright file="VramBucketBudget.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Per-bucket VRAM budget descriptor inside an <see cref="IVramLayout"/>.
/// </summary>
/// <param name="Minimum">
/// Guaranteed floor in bytes — the scheduler must not pre-empt this bucket
/// below this value.
/// </param>
/// <param name="Budget">
/// Soft ceiling in bytes — allocation requests above this value are rejected
/// by <c>VramBudgetMonitor.CanAllocate*</c> helpers.
/// </param>
/// <param name="Priority">
/// Pre-emption priority — higher values win during contention. Reserved for
/// a future scheduler pass; no runtime behaviour is keyed on it today.
/// </param>
public sealed record VramBucketBudget(long Minimum, long Budget, int Priority);
