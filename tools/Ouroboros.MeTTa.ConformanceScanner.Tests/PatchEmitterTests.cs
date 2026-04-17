// <copyright file="PatchEmitterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.MeTTa.ConformanceScanner.Patches;
using Xunit;
using MismatchRow = Ouroboros.MeTTa.ConformanceScanner.Mismatch.Mismatch;
using MismatchKind = Ouroboros.MeTTa.ConformanceScanner.Mismatch.MismatchKind;

namespace Ouroboros.MeTTa.ConformanceScanner.Tests;

public sealed class PatchEmitterTests
{
    [Fact]
    public void Emit_produces_one_proposal_per_mismatch()
    {
        var mismatches = new MismatchRow[]
        {
            new("x", MismatchKind.MissingInEngine, null, null, string.Empty),
            new("y", MismatchKind.ExtraInEngine, null, "tools-engine", "note"),
        };
        var proposals = PatchEmitter.Emit(mismatches);
        Assert.Equal(2, proposals.Count);
        Assert.Contains(proposals, p => p.Category == "stdlib-grounded");
        Assert.Contains(proposals, p => p.Category == "extension");
    }
}
