// <copyright file="ConfidenceRating.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Agent;

/// <summary>
/// Represents the confidence level of an agent's response.
/// </summary>
public enum ConfidenceRating
{
    /// <summary>
    /// Low confidence - response may need significant revision.
    /// </summary>
    Low,

    /// <summary>
    /// Medium confidence - response is acceptable but could be improved.
    /// </summary>
    Medium,

    /// <summary>
    /// High confidence - response meets quality standards.
    /// </summary>
    High,
}
