// <copyright file="DigestFragment.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Compressed output after a NanoOuroborosAtom's self-consumption (the ouroboros bite).
/// Contains the digested thought with compression metrics and confidence scoring.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="SourceAtomId">The NanoAtom that produced this digest.</param>
/// <param name="Content">The compressed thought content.</param>
/// <param name="CompressionRatio">Ratio of original tokens to digest tokens (higher = more compression).</param>
/// <param name="Confidence">Confidence score from 0.0 to 1.0 (from SelfCritique pattern).</param>
/// <param name="CompletedPhase">The phase the atom was in when this digest was produced.</param>
/// <param name="Timestamp">When this digest was created.</param>
public sealed record DigestFragment(
    Guid Id,
    Guid SourceAtomId,
    string Content,
    double CompressionRatio,
    double Confidence,
    NanoAtomPhase CompletedPhase,
    DateTime Timestamp);
