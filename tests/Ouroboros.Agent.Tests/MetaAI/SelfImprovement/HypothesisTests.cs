// <copyright file="HypothesisTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HypothesisTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var statement = "Larger models generalize better";
        var domain = "ML";
        var confidence = 0.75;
        var supporting = new List<string> { "GPT-4 benchmarks" };
        var counter = new List<string> { "Scaling limits" };
        var createdAt = DateTime.UtcNow;

        // Act
        var hypothesis = new Hypothesis(id, statement, domain, confidence, supporting, counter, createdAt, false, null);

        // Assert
        hypothesis.Id.Should().Be(id);
        hypothesis.Statement.Should().Be(statement);
        hypothesis.Domain.Should().Be(domain);
        hypothesis.Confidence.Should().Be(confidence);
        hypothesis.SupportingEvidence.Should().BeEquivalentTo(supporting);
        hypothesis.CounterEvidence.Should().BeEquivalentTo(counter);
        hypothesis.CreatedAt.Should().Be(createdAt);
        hypothesis.Tested.Should().BeFalse();
        hypothesis.Validated.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithTestedAndValidated_SetsCorrectly()
    {
        var hypothesis = new Hypothesis(
            Guid.NewGuid(), "test", "domain", 0.5,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, true, true);

        hypothesis.Tested.Should().BeTrue();
        hypothesis.Validated.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithTestedAndInvalidated_SetsCorrectly()
    {
        var hypothesis = new Hypothesis(
            Guid.NewGuid(), "test", "domain", 0.5,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, true, false);

        hypothesis.Tested.Should().BeTrue();
        hypothesis.Validated.Should().BeFalse();
    }

    [Fact]
    public void With_CanUpdateConfidence()
    {
        var original = new Hypothesis(
            Guid.NewGuid(), "test", "domain", 0.5,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, false, null);

        var updated = original with { Confidence = 0.9 };

        updated.Confidence.Should().Be(0.9);
        updated.Statement.Should().Be(original.Statement);
    }

    [Fact]
    public void With_CanMarkAsTested()
    {
        var original = new Hypothesis(
            Guid.NewGuid(), "test", "domain", 0.5,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, false, null);

        var tested = original with { Tested = true, Validated = true };

        tested.Tested.Should().BeTrue();
        tested.Validated.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameId_WithSameFields_AreEqual()
    {
        var id = Guid.NewGuid();
        var time = DateTime.UtcNow;
        var supporting = new List<string>();
        var counter = new List<string>();

        var a = new Hypothesis(id, "stmt", "domain", 0.5, supporting, counter, time, false, null);
        var b = new Hypothesis(id, "stmt", "domain", 0.5, supporting, counter, time, false, null);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var time = DateTime.UtcNow;
        var a = new Hypothesis(Guid.NewGuid(), "stmt", "domain", 0.5, new List<string>(), new List<string>(), time, false, null);
        var b = new Hypothesis(Guid.NewGuid(), "stmt", "domain", 0.5, new List<string>(), new List<string>(), time, false, null);

        a.Should().NotBe(b);
    }
}
