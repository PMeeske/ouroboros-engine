// <copyright file="TaskAppraisalTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public sealed class TaskAppraisalTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var appraisal = new TaskAppraisal(0.8, 0.6, 0.9, 0.7, "urgent and relevant");

        // Assert
        appraisal.ThreatLevel.Should().Be(0.8);
        appraisal.OpportunityScore.Should().Be(0.6);
        appraisal.UrgencyFactor.Should().Be(0.9);
        appraisal.RelevanceScore.Should().Be(0.7);
        appraisal.Rationale.Should().Be("urgent and relevant");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new TaskAppraisal(0.5, 0.5, 0.5, 0.5, "neutral");
        var b = new TaskAppraisal(0.5, 0.5, 0.5, 0.5, "neutral");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentValues()
    {
        // Arrange
        var a = new TaskAppraisal(0.1, 0.2, 0.3, 0.4, "low");
        var b = new TaskAppraisal(0.9, 0.8, 0.7, 0.6, "high");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var appraisal = new TaskAppraisal(0.5, 0.5, 0.5, 0.5, "original");

        // Act
        var modified = appraisal with { ThreatLevel = 1.0, Rationale = "escalated" };

        // Assert
        modified.ThreatLevel.Should().Be(1.0);
        modified.Rationale.Should().Be("escalated");
        modified.OpportunityScore.Should().Be(0.5);
    }
}
