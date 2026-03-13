// <copyright file="ParseFailureInfo.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Information about a parse failure used to drive grammar refinement.
/// </summary>
public sealed record ParseFailureInfo(
    string OffendingToken,
    string ExpectedTokens,
    int Line,
    int Column,
    string InputSnippet);
