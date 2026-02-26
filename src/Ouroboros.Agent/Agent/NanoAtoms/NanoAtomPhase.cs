// <copyright file="NanoAtomPhase.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Represents the phases of a NanoOuroborosAtom's self-consuming cycle.
/// Mirrors the Ouroboros improvement cycle (Plan→Execute→Verify→Learn)
/// at nano scale: Receive→Process→Digest→Emit.
/// </summary>
public enum NanoAtomPhase
{
    /// <summary>Atom is idle, waiting for input.</summary>
    Idle,

    /// <summary>Accepting a ThoughtFragment.</summary>
    Receive,

    /// <summary>LLM call within token budget.</summary>
    Process,

    /// <summary>Self-consuming: compressing own output (the ouroboros bite).</summary>
    Digest,

    /// <summary>Outputting DigestFragment downstream.</summary>
    Emit,
}
