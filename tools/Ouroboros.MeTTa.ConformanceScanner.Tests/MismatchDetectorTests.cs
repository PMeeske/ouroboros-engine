// <copyright file="MismatchDetectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.MeTTa.ConformanceScanner.Inventory;
using Ouroboros.MeTTa.ConformanceScanner.Mismatch;
using Ouroboros.MeTTa.ConformanceScanner.Spec;
using Xunit;

namespace Ouroboros.MeTTa.ConformanceScanner.Tests;

public sealed class MismatchDetectorTests
{
    [Fact]
    public void Detect_always_emits_four_semantic_drifts()
    {
        var spec = new ParsedSpec(
            new Dictionary<string, SpecSchema>(StringComparer.Ordinal),
            0,
            0,
            "abc");
        var inv = new EngineInventory(
            Array.Empty<RegisteredOperation>(),
            Array.Empty<RegisteredOperation>(),
            Array.Empty<RegisteredOperation>(),
            Array.Empty<RegisteredOperation>());
        var mismatches = MismatchDetector.Detect(spec, inv);
        var drift = mismatches.Where(m => m.Kind == MismatchKind.SemanticDrift).ToList();
        Assert.Equal(4, drift.Count);
        Assert.Contains(drift, m => m.OpName == "bind!");
        Assert.Contains(drift, m => m.OpName == "match");
        Assert.Contains(drift, m => m.OpName == "cons");
        Assert.Contains(drift, m => m.OpName == "import!");
    }
}
