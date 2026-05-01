// <copyright file="LawsOfFormRegistration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// One-call registration helper for the Laws of Form grounded ops.
/// </summary>
public static class LawsOfFormRegistration
{
    /// <summary>
    /// Registers <c>cross</c>, <c>call</c>, and <c>reentry</c> in the given
    /// registry, all backed by a shared <see cref="DistinctionTracker"/>.
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="tracker">The shared distinction tracker.</param>
    public static void RegisterAll(GroundedRegistry registry, DistinctionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tracker);

        CrossOperation cross = new(tracker);
        CallOperation call = new(tracker);
        ReentryOperation reentry = new(tracker);

        registry.Register(CrossOperation.Name, cross.AsGroundedOperation());
        registry.Register(CallOperation.Name, call.AsGroundedOperation());
        registry.Register(ReentryOperation.Name, reentry.AsGroundedOperation());
    }
}
