// <copyright file="BayesianConfidenceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class BayesianConfidenceTests
{
    // ── Update ──────────────────────────────────────────────────────

    [Fact]
    public void Update_WithSupportingEvidence_IncreasesConfidence()
    {
        // Arrange
        double prior = 0.5;
        double likelihoodIfTrue = 0.9;
        double likelihoodIfFalse = 0.1;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        posterior.Should().BeGreaterThan(prior);
    }

    [Fact]
    public void Update_WithContradictingEvidence_DecreasesConfidence()
    {
        // Arrange
        double prior = 0.5;
        double likelihoodIfTrue = 0.1;
        double likelihoodIfFalse = 0.9;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        posterior.Should().BeLessThan(prior);
    }

    [Fact]
    public void Update_WithUninformativeEvidence_ReturnsNearPrior()
    {
        // Arrange — equal likelihoods yield no update
        double prior = 0.6;
        double likelihoodIfTrue = 0.5;
        double likelihoodIfFalse = 0.5;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        posterior.Should().BeApproximately(prior, 0.01);
    }

    [Fact]
    public void Update_ClampsToMinimum()
    {
        // Arrange — very strong contradicting evidence with low prior
        double prior = 0.01;
        double likelihoodIfTrue = 0.001;
        double likelihoodIfFalse = 0.999;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        posterior.Should().BeGreaterThanOrEqualTo(0.001);
    }

    [Fact]
    public void Update_ClampsToMaximum()
    {
        // Arrange — very strong supporting evidence with high prior
        double prior = 0.99;
        double likelihoodIfTrue = 0.999;
        double likelihoodIfFalse = 0.001;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        posterior.Should().BeLessThanOrEqualTo(0.999);
    }

    [Fact]
    public void Update_WithZeroEvidence_ReturnsPrior()
    {
        // Arrange — P(E) = 0 => no update
        double prior = 0.7;

        // Act
        double posterior = BayesianConfidence.Update(prior, 0.0, 0.0);

        // Assert
        posterior.Should().Be(prior);
    }

    [Theory]
    [InlineData(0.5, 0.8, 0.2)]
    [InlineData(0.3, 0.9, 0.1)]
    [InlineData(0.8, 0.7, 0.4)]
    public void Update_FollowsBayesTheorem(double prior, double likelihoodTrue, double likelihoodFalse)
    {
        // Arrange
        double pEvidence = likelihoodTrue * prior + likelihoodFalse * (1.0 - prior);
        double expectedPosterior = (likelihoodTrue * prior) / pEvidence;
        expectedPosterior = Math.Clamp(expectedPosterior, 0.001, 0.999);

        // Act
        double actual = BayesianConfidence.Update(prior, likelihoodTrue, likelihoodFalse);

        // Assert
        actual.Should().BeApproximately(expectedPosterior, 0.0001);
    }

    // ── BayesFactor ─────────────────────────────────────────────────

    [Fact]
    public void BayesFactor_WithSupportingEvidence_ReturnsGreaterThanOne()
    {
        // Act
        double factor = BayesianConfidence.BayesFactor(0.9, 0.1);

        // Assert
        factor.Should().Be(9.0);
    }

    [Fact]
    public void BayesFactor_WithContradictingEvidence_ReturnsLessThanOne()
    {
        // Act
        double factor = BayesianConfidence.BayesFactor(0.1, 0.9);

        // Assert
        factor.Should().BeApproximately(1.0 / 9.0, 0.0001);
    }

    [Fact]
    public void BayesFactor_WithZeroLikelihoodFalse_ReturnsPositiveInfinity()
    {
        // Act
        double factor = BayesianConfidence.BayesFactor(0.5, 0.0);

        // Assert
        factor.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void BayesFactor_WithEqualLikelihoods_ReturnsOne()
    {
        // Act
        double factor = BayesianConfidence.BayesFactor(0.5, 0.5);

        // Assert
        factor.Should().Be(1.0);
    }

    // ── CategorizeEvidence ──────────────────────────────────────────

    [Fact]
    public void CategorizeEvidence_NegativeBayesFactor_ThrowsArgumentOutOfRange()
    {
        // Act
        var act = () => BayesianConfidence.CategorizeEvidence(-1.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CategorizeEvidence_ZeroBayesFactor_ThrowsArgumentOutOfRange()
    {
        // Act
        var act = () => BayesianConfidence.CategorizeEvidence(0.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CategorizeEvidence_NaN_ThrowsArgumentOutOfRange()
    {
        // Act
        var act = () => BayesianConfidence.CategorizeEvidence(double.NaN);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CategorizeEvidence_PositiveInfinity_ReturnsDecisive()
    {
        // Act
        var result = BayesianConfidence.CategorizeEvidence(double.PositiveInfinity);

        // Assert
        result.Should().Be(EvidenceStrength.Decisive);
    }

    [Fact]
    public void CategorizeEvidence_NearOne_ReturnsNegligible()
    {
        // Arrange — log10(1.5) ≈ 0.176 < 0.5 => Negligible
        var result = BayesianConfidence.CategorizeEvidence(1.5);

        // Assert
        result.Should().Be(EvidenceStrength.Negligible);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor5_ReturnsSubstantial()
    {
        // Arrange — log10(5) ≈ 0.699 => Substantial (0.5–1.0)
        var result = BayesianConfidence.CategorizeEvidence(5.0);

        // Assert
        result.Should().Be(EvidenceStrength.Substantial);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor15_ReturnsStrong()
    {
        // Arrange — log10(15) ≈ 1.176 => Strong (1.0–1.5)
        var result = BayesianConfidence.CategorizeEvidence(15.0);

        // Assert
        result.Should().Be(EvidenceStrength.Strong);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor50_ReturnsVeryStrong()
    {
        // Arrange — log10(50) ≈ 1.699 => VeryStrong (1.5–2.0)
        var result = BayesianConfidence.CategorizeEvidence(50.0);

        // Assert
        result.Should().Be(EvidenceStrength.VeryStrong);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor200_ReturnsDecisive()
    {
        // Arrange — log10(200) ≈ 2.301 => Decisive (>= 2.0)
        var result = BayesianConfidence.CategorizeEvidence(200.0);

        // Assert
        result.Should().Be(EvidenceStrength.Decisive);
    }

    [Fact]
    public void CategorizeEvidence_SmallBayesFactor_UsesAbsoluteLogScale()
    {
        // Arrange — BF = 0.01 => |log10(0.01)| = 2.0 => Decisive
        var result = BayesianConfidence.CategorizeEvidence(0.01);

        // Assert
        result.Should().Be(EvidenceStrength.Decisive);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactorOneThird_ReturnsSubstantial()
    {
        // Arrange — BF = 0.2 => |log10(0.2)| ≈ 0.699 => Substantial
        var result = BayesianConfidence.CategorizeEvidence(0.2);

        // Assert
        result.Should().Be(EvidenceStrength.Substantial);
    }
}
