// <copyright file="SelfAssessmentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class SelfAssessmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var overallPerformance = 0.85;
        var calibration = 0.72;
        var acquisitionRate = 0.6;
        var capabilityScores = new Dictionary<string, double>
        {
            ["reasoning"] = 0.9,
            ["creativity"] = 0.7
        };
        var strengths = new List<string> { "Logical reasoning", "Pattern recognition" };
        var weaknesses = new List<string> { "Creative writing" };
        var assessmentTime = DateTime.UtcNow;
        var summary = "Overall strong performance with room for improvement in creative tasks.";

        // Act
        var assessment = new SelfAssessment(
            overallPerformance, calibration, acquisitionRate,
            capabilityScores, strengths, weaknesses,
            assessmentTime, summary);

        // Assert
        assessment.OverallPerformance.Should().Be(overallPerformance);
        assessment.ConfidenceCalibration.Should().Be(calibration);
        assessment.SkillAcquisitionRate.Should().Be(acquisitionRate);
        assessment.CapabilityScores.Should().BeEquivalentTo(capabilityScores);
        assessment.Strengths.Should().BeEquivalentTo(strengths);
        assessment.Weaknesses.Should().BeEquivalentTo(weaknesses);
        assessment.AssessmentTime.Should().Be(assessmentTime);
        assessment.Summary.Should().Be(summary);
    }

    [Fact]
    public void Constructor_WithEmptyLists_Succeeds()
    {
        var assessment = new SelfAssessment(
            0.5, 0.5, 0.5,
            new Dictionary<string, double>(),
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow, "Empty assessment");

        assessment.CapabilityScores.Should().BeEmpty();
        assessment.Strengths.Should().BeEmpty();
        assessment.Weaknesses.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = DateTime.UtcNow;
        var scores = new Dictionary<string, double>();
        var strengths = new List<string>();
        var weaknesses = new List<string>();

        var a = new SelfAssessment(0.5, 0.5, 0.5, scores, strengths, weaknesses, time, "summary");
        var b = new SelfAssessment(0.5, 0.5, 0.5, scores, strengths, weaknesses, time, "summary");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentPerformance_AreNotEqual()
    {
        var time = DateTime.UtcNow;
        var scores = new Dictionary<string, double>();

        var a = new SelfAssessment(0.5, 0.5, 0.5, scores, new List<string>(), new List<string>(), time, "s");
        var b = new SelfAssessment(0.9, 0.5, 0.5, scores, new List<string>(), new List<string>(), time, "s");

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new SelfAssessment(
            0.5, 0.5, 0.5, new Dictionary<string, double>(),
            new List<string>(), new List<string>(),
            DateTime.UtcNow, "original");

        var modified = original with { Summary = "modified" };

        modified.Summary.Should().Be("modified");
        modified.OverallPerformance.Should().Be(original.OverallPerformance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_AcceptsVariousPerformanceValues(double performance)
    {
        var assessment = new SelfAssessment(
            performance, 0.5, 0.5, new Dictionary<string, double>(),
            new List<string>(), new List<string>(),
            DateTime.UtcNow, "test");

        assessment.OverallPerformance.Should().Be(performance);
    }
}
