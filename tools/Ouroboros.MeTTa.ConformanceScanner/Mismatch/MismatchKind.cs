// <copyright file="MismatchKind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Mismatch;

/// <summary>
/// Classification of spec↔engine differences (four kinds, CONTEXT.md).
/// </summary>
public enum MismatchKind
{
    MissingInEngine,
    ExtraInEngine,
    SignatureMismatch,
    SemanticDrift,
}
