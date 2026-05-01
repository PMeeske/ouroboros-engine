// <copyright file="EthicsConflictResolverTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.MeTTa.Ethics;
using Xunit;

namespace Ouroboros.MeTTa.Tests.Ethics;

public sealed class EthicsConflictResolverTests
{
    [Fact]
    public void Resolve_WithUnanimousApprove_ReturnsApprove()
    {
        EthicsConflictResolver resolver = new();
        var votes = new[]
        {
            new EthicsVote(EthicsTradition.CoreEthics, EthicsVerdict.Approve, 1.0),
            new EthicsVote(EthicsTradition.Ahimsa, EthicsVerdict.Approve, 1.0),
            new EthicsVote(EthicsTradition.Kantian, EthicsVerdict.Approve, 1.0),
        };

        var resolution = resolver.Resolve(votes);

        resolution.Verdict.Should().Be(EthicsVerdict.Approve);
        resolution.Confidence.Should().BeGreaterThan(0.4);
    }

    [Fact]
    public void Resolve_WithSplitVote_EscalatesToHuman()
    {
        EthicsConflictResolver resolver = new();
        var votes = new[]
        {
            new EthicsVote(EthicsTradition.Kantian, EthicsVerdict.Approve, 1.0),
            new EthicsVote(EthicsTradition.Ahimsa, EthicsVerdict.Reject, 1.0),
        };

        var resolution = resolver.Resolve(votes);

        resolution.RequiresHumanEscalation.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithEmptyVotes_EscalatesAndIsIndeterminate()
    {
        EthicsConflictResolver resolver = new();
        var resolution = resolver.Resolve(Array.Empty<EthicsVote>());

        resolution.Verdict.Should().Be(EthicsVerdict.Indeterminate);
        resolution.RequiresHumanEscalation.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithLowPhi_DampensConfidence()
    {
        EthicsConflictResolver resolver = new();
        var votes = new[]
        {
            new EthicsVote(EthicsTradition.CoreEthics, EthicsVerdict.Approve, 1.0),
            new EthicsVote(EthicsTradition.Ahimsa, EthicsVerdict.Approve, 1.0),
        };

        var fullPhi = resolver.Resolve(votes, phiProxy: 1.0);
        var lowPhi = resolver.Resolve(votes, phiProxy: 0.1);

        lowPhi.Confidence.Should().BeLessThan(fullPhi.Confidence);
    }
}
