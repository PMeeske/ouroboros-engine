// <copyright file="Mismatch.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Mismatch;

/// <summary>
/// A single spec↔engine difference.
/// </summary>
public sealed record Mismatch(
    string OpName,
    MismatchKind Kind,
    string? SpecSignature,
    string? EngineSource,
    string Notes);
