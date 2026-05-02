// <copyright file="DistinctionState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// The 3-valued state of a distinction in Spencer-Brown's Laws of Form.
/// </summary>
public enum DistinctionState
{
    /// <summary>The unmarked state — nothing distinguished.</summary>
    Void,

    /// <summary>The marked state — a distinction has been drawn.</summary>
    Mark,

    /// <summary>The re-entry state — the distinction refers to itself.</summary>
    Imaginary,
}
