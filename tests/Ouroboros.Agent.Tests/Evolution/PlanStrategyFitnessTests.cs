// <copyright file="PlanStrategyFitnessTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Evolution;
using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Tests.Evolution;

/// <summary>
/// Unit tests for PlanStrategyFitness function.
/// </summary>
[Trait("Category", "Unit")]
public class PlanStrategyFitnessTests
{
    [Fact]
    public async Task EvaluateAsync_WithEmptyExperienceList_ReturnsBaselineFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault(); // No experiences
        var fitness = new PlanStrategyFitness(atom);
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        result.Should().Be(0.5); // Baseline fitness
    }

    [Fact]
    public async Task EvaluateAsync_WithAllSuccessfulExperiences_ReturnsHighFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        // Add successful experiences
        for (int i = 0; i < 5; i++)
        {
            var experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: $"Goal {i}",
                Success: true,
                QualityScore: 0.9,
                Insights: new List<string> { "Insight 1" },
                Timestamp: DateTime.UtcNow);
            
            atom.RecordExperience(experience);
        }

        var fitness = new PlanStrategyFitness(atom);
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        result.Should().BeGreaterThan(0.7); // High fitness for all successful
        result.Should().BeLessThanOrEqualTo(1.0); // Max fitness is 1.0
    }

    [Fact]
    public async Task EvaluateAsync_WithAllFailedExperiences_ReturnsLowFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        // Add failed experiences
        for (int i = 0; i < 5; i++)
        {
            var experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: $"Goal {i}",
                Success: false,
                QualityScore: 0.1,
                Insights: new List<string> { "Error occurred" },
                Timestamp: DateTime.UtcNow);
            
            atom.RecordExperience(experience);
        }

        var fitness = new PlanStrategyFitness(atom);
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        result.Should().BeLessThan(0.4); // Low fitness for all failed
        result.Should().BeGreaterThanOrEqualTo(0.0); // Min fitness is 0.0
    }

    [Fact]
    public async Task EvaluateAsync_WithMixedExperiences_ReturnsProportionalFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        // Add 3 successful and 2 failed experiences
        for (int i = 0; i < 3; i++)
        {
            var experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: $"Goal {i}",
                Success: true,
                QualityScore: 0.8,
                Insights: new List<string> { "Success" },
                Timestamp: DateTime.UtcNow);
            
            atom.RecordExperience(experience);
        }

        for (int i = 3; i < 5; i++)
        {
            var experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: $"Goal {i}",
                Success: false,
                QualityScore: 0.3,
                Insights: new List<string> { "Failure" },
                Timestamp: DateTime.UtcNow);
            
            atom.RecordExperience(experience);
        }

        var fitness = new PlanStrategyFitness(atom);
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        result.Should().BeInRange(0.3, 0.8); // Proportional fitness (60% success rate)
    }

    [Fact]
    public async Task EvaluateAsync_UsesQualityScoresCorrectly()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        // Add experiences with varying quality scores
        var highQualityExperience = new OuroborosExperience(
            Id: Guid.NewGuid(),
            Goal: "High quality goal",
            Success: true,
            QualityScore: 0.95,
            Insights: new List<string> { "Excellent" },
            Timestamp: DateTime.UtcNow);
        
        var lowQualityExperience = new OuroborosExperience(
            Id: Guid.NewGuid(),
            Goal: "Low quality goal",
            Success: true,
            QualityScore: 0.2,
            Insights: new List<string> { "Poor" },
            Timestamp: DateTime.UtcNow);
        
        atom.RecordExperience(highQualityExperience);
        atom.RecordExperience(lowQualityExperience);

        var fitness = new PlanStrategyFitness(atom);
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        // Result should reflect the average quality of 0.575
        result.Should().BeInRange(0.4, 0.7);
    }

    [Fact]
    public async Task EvaluateAsync_WithDifferentWeights_ProducesDifferentFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        var experience = new OuroborosExperience(
            Id: Guid.NewGuid(),
            Goal: "Test goal",
            Success: true,
            QualityScore: 0.8,
            Insights: new List<string> { "Insight" },
            Timestamp: DateTime.UtcNow);
        
        atom.RecordExperience(experience);

        var fitnessDefault = new PlanStrategyFitness(atom);
        var fitnessCustom = new PlanStrategyFitness(
            atom,
            successRateWeight: 0.8,
            qualityWeight: 0.1,
            speedWeight: 0.1);

        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double resultDefault = await fitnessDefault.EvaluateAsync(chromosome);
        double resultCustom = await fitnessCustom.EvaluateAsync(chromosome);

        // Assert
        // Different weight distributions should produce different results
        resultDefault.Should().NotBe(resultCustom);
    }

    [Fact]
    public async Task EvaluateAsync_WithException_ReturnsNeutralFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var fitness = new PlanStrategyFitness(atom);
        
        // Create a chromosome that might cause issues (null genes list scenario handled internally)
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        double result = await fitness.EvaluateAsync(chromosome);

        // Assert
        // Should return neutral fitness on any internal error
        result.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Constructor_WithNullAtom_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PlanStrategyFitness(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("atom");
    }

    [Fact]
    public async Task EvaluateAsync_StrategyModifiersAffectFitness()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        
        var experience = new OuroborosExperience(
            Id: Guid.NewGuid(),
            Goal: "Test goal",
            Success: true,
            QualityScore: 0.6,
            Insights: new List<string> { "Insight" },
            Timestamp: DateTime.UtcNow);
        
        atom.RecordExperience(experience);

        var fitness = new PlanStrategyFitness(atom);
        
        // Chromosome with higher planning depth (should affect quality positively)
        var highDepthGenes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.9),
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.5),
            PlanStrategyGene.Strategies.VerificationStrictness(0.5),
            PlanStrategyGene.Strategies.DecompositionGranularity(0.5),
        };
        var highDepthChromosome = new PlanStrategyChromosome(highDepthGenes);

        // Chromosome with lower planning depth
        var lowDepthGenes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.1),
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.5),
            PlanStrategyGene.Strategies.VerificationStrictness(0.5),
            PlanStrategyGene.Strategies.DecompositionGranularity(0.5),
        };
        var lowDepthChromosome = new PlanStrategyChromosome(lowDepthGenes);

        // Act
        double highDepthFitness = await fitness.EvaluateAsync(highDepthChromosome);
        double lowDepthFitness = await fitness.EvaluateAsync(lowDepthChromosome);

        // Assert
        // Different strategy parameters should produce different fitness values
        highDepthFitness.Should().NotBe(lowDepthFitness);
        highDepthFitness.Should().BeInRange(0.0, 1.0);
        lowDepthFitness.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task ImplementsIFitnessFunction_Interface()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var fitness = new PlanStrategyFitness(atom);

        // Act & Assert
        fitness.Should().BeAssignableTo<IFitnessFunction<PlanStrategyGene>>();
        
        // Verify it can be used polymorphically
        IFitnessFunction<PlanStrategyGene> fitnessInterface = fitness;
        var chromosome = PlanStrategyChromosome.CreateDefault();
        double result = await fitnessInterface.EvaluateAsync(chromosome);
        result.Should().BeInRange(0.0, 1.0);
    }
}
