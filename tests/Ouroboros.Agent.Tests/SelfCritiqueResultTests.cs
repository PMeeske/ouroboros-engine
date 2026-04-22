// <copyright file="SelfCritiqueResultTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for the <see cref="SelfCritiqueResult"/> record.
/// Covers construction, property initialization, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class SelfCritiqueResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = new SelfCritiqueResult(
            Draft: "Initial draft",
            Critique: "Needs more detail",
            ImprovedResponse: "Improved draft with detail",
            Confidence: ConfidenceRating.High,
            IterationsPerformed: 3,
            Branch: branch);

        // Assert
        result.Draft.Should().Be("Initial draft");
        result.Critique.Should().Be("Needs more detail");
        result.ImprovedResponse.Should().Be("Improved draft with detail");
        result.Confidence.Should().Be(ConfidenceRating.High);
        result.IterationsPerformed.Should().Be(3);
        result.Branch.Should().Be(branch);
    }

    [Fact]
    public void Constructor_WithLowConfidence_SetsCorrectly()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = new SelfCritiqueResult(
            "draft", "critique", "improved",
            ConfidenceRating.Low, 1, branch);

        // Assert
        result.Confidence.Should().Be(ConfidenceRating.Low);
        result.IterationsPerformed.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithMediumConfidence_SetsCorrectly()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = new SelfCritiqueResult(
            "draft", "critique", "improved",
            ConfidenceRating.Medium, 2, branch);

        // Assert
        result.Confidence.Should().Be(ConfidenceRating.Medium);
    }

    [Fact]
    public void Constructor_ZeroIterations_IsAllowed()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = new SelfCritiqueResult(
            "draft", "critique", "improved",
            ConfidenceRating.Low, 0, branch);

        // Assert
        result.IterationsPerformed.Should().Be(0);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var branch = CreateBranch();
        var a = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.High, 2, branch);
        var b = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.High, 2, branch);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentDraft_AreNotEqual()
    {
        // Arrange
        var branch = CreateBranch();
        var a = new SelfCritiqueResult("draft-A", "c", "i", ConfidenceRating.High, 2, branch);
        var b = new SelfCritiqueResult("draft-B", "c", "i", ConfidenceRating.High, 2, branch);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentConfidence_AreNotEqual()
    {
        // Arrange
        var branch = CreateBranch();
        var a = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.High, 2, branch);
        var b = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.Low, 2, branch);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentIterations_AreNotEqual()
    {
        // Arrange
        var branch = CreateBranch();
        var a = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.Medium, 1, branch);
        var b = new SelfCritiqueResult("d", "c", "i", ConfidenceRating.Medium, 5, branch);

        // Assert
        a.Should().NotBe(b);
    }

    private static PipelineBranch CreateBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = Ouroboros.Domain.Vectors.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test-branch", store, dataSource);
    }
}
