// <copyright file="ConsolidatedAction.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Merged result from multiple thought streams after consolidation.
/// Represents the final unified action produced by the NanoAtomChain.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Content">The consolidated response content.</param>
/// <param name="SourceDigests">The digest fragments that contributed to this action.</param>
/// <param name="Confidence">Aggregate confidence score from 0.0 to 1.0.</param>
/// <param name="ActionType">Type of action: "response", "plan", "critique", "code".</param>
/// <param name="StreamCount">Number of parallel streams that contributed.</param>
/// <param name="ElapsedMs">Total processing time in milliseconds.</param>
/// <param name="Timestamp">When this action was produced.</param>
public sealed record ConsolidatedAction(
    Guid Id,
    string Content,
    IReadOnlyList<DigestFragment> SourceDigests,
    double Confidence,
    string ActionType,
    int StreamCount,
    long ElapsedMs,
    DateTime Timestamp);
