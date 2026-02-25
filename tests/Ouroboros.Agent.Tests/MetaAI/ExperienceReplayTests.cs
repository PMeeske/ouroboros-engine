// <copyright file="ExperienceReplayTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Core;
using MemoryStatistics = Ouroboros.Agent.MetaAI.MemoryStatistics;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;
using Skill = Ouroboros.Agent.MetaAI.Skill;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the ExperienceReplay continual learning component.
/// </summary>
[Trait("Category", "Unit")]
public class ExperienceReplayTests
{
    private readonly Mock<IMemoryStore> _mockMemory;
    private readonly Mock<ISkillRegistry> _mockSkills;
    private readonly Mock<IChatCompletionModel> _mockLlm;
    private readonly ExperienceReplay _experienceReplay;

    public ExperienceReplayTests()
    {
        _mockMemory = new Mock<IMemoryStore>();
        _mockSkills = new Mock<ISkillRegistry>();
        _mockLlm = new Mock<IChatCompletionModel>();
        _experienceReplay = new ExperienceReplay(
            _mockMemory.Object,
            _mockSkills.Object,
            _mockLlm.Object);
    }

    private static Experience CreateTestExperience(
        string goal,
        bool verified,
        double qualityScore,
        List<PlanStep>? planSteps = null)
    {
        var steps = planSteps ?? new List<PlanStep>
        {
            new PlanStep("analyze", new Dictionary<string, object>(), "done", 0.9)
        };

        var plan = new Plan(
            goal,
            steps,
            new Dictionary<string, double> { ["overall"] = 0.8 },
            DateTime.UtcNow);

        var stepResults = steps.Select(s => new StepResult(
            s,
            true,
            "output",
            null,
            TimeSpan.FromMilliseconds(100),
            new Dictionary<string, object>())).ToList();

        var execution = new PlanExecutionResult(
            plan,
            stepResults,
            true,
            "Final output",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(1));

        var verification = new PlanVerificationResult(
            execution,
            verified,
            qualityScore,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow);

        return new Experience(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            "test_context",
            "test_action",
            "test_outcome",
            verified,
            new List<string> { "test" },
            goal,
            execution,
            verification,
            plan);
    }

    [Fact]
    public async Task TrainOnExperiencesAsync_NoExperiences_ReturnsSuccessWithZeroProcessed()
    {
        // Arrange
        _mockMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(0, 0, 0, 0, 0)));

        _mockMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(
                new List<Experience>()));

        // Act
        var result = await _experienceReplay.TrainOnExperiencesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExperiencesProcessed.Should().Be(0);
        result.Value.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TrainOnExperiencesAsync_WithHighQualityExperiences_ExtractsSkills()
    {
        // Arrange
        var experiences = new List<Experience>
        {
            CreateTestExperience("calculate sum", verified: true, qualityScore: 0.9),
            CreateTestExperience("calculate product", verified: true, qualityScore: 0.85)
        };

        _mockMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(2, 2, 0, 1, 1)));

        _mockMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(
                (IReadOnlyList<Experience>)experiences));

        _mockSkills
            .Setup(s => s.ExtractSkillAsync(
                It.IsAny<PlanExecutionResult>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Skill, string>.Success(
                new Skill("learned", "desc", new List<string>(), new List<PlanStep>(), 0.9, 0, DateTime.UtcNow, DateTime.UtcNow)));

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("- Common pattern: analyze then compute");

        // Act
        var result = await _experienceReplay.TrainOnExperiencesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExperiencesProcessed.Should().Be(2);
        result.Value.ImprovedMetrics.Should().ContainKey("skills_extracted");
        result.Value.ImprovedMetrics["skills_extracted"].Should().Be(2);
    }

    [Fact]
    public async Task SelectTrainingExperiencesAsync_PrioritizesHighQuality_ReturnsSortedByQuality()
    {
        // Arrange
        var config = new ExperienceReplayConfig(
            BatchSize: 2,
            MinQualityScore: 0.5,
            MaxExperiences: 10,
            PrioritizeHighQuality: true);

        var experiences = new List<Experience>
        {
            CreateTestExperience("low quality task", verified: true, qualityScore: 0.6),
            CreateTestExperience("high quality task", verified: true, qualityScore: 0.95),
            CreateTestExperience("medium quality task", verified: true, qualityScore: 0.75)
        };

        _mockMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(3, 3, 0, 1, 1)));

        _mockMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(
                (IReadOnlyList<Experience>)experiences));

        // Act
        var selected = await _experienceReplay.SelectTrainingExperiencesAsync(config);

        // Assert
        selected.Should().HaveCount(2);
        selected[0].Verification.QualityScore.Should().BeGreaterThanOrEqualTo(
            selected[1].Verification.QualityScore);
    }

    [Fact]
    public async Task AnalyzeExperiencePatternsAsync_WithGroupableGoals_FindsPatterns()
    {
        // Arrange
        var experiences = new List<Experience>
        {
            CreateTestExperience(
                "calculate the total",
                verified: true,
                qualityScore: 0.9,
                new List<PlanStep>
                {
                    new PlanStep("parse_input", new Dictionary<string, object>(), "parsed", 0.9),
                    new PlanStep("compute", new Dictionary<string, object>(), "computed", 0.9)
                }),
            CreateTestExperience(
                "calculate the average",
                verified: true,
                qualityScore: 0.85,
                new List<PlanStep>
                {
                    new PlanStep("parse_input", new Dictionary<string, object>(), "parsed", 0.9),
                    new PlanStep("compute", new Dictionary<string, object>(), "computed", 0.85)
                })
        };

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("- Parse then compute is a repeated strategy\n- Input validation improves outcomes");

        // Act
        var patterns = await _experienceReplay.AnalyzeExperiencePatternsAsync(experiences);

        // Assert
        patterns.Should().NotBeEmpty();
        patterns.Should().Contain(p => p.Contains("calculation"));
    }

    [Fact]
    public async Task TrainOnExperiencesAsync_MemoryStoreThrows_ReturnsFailure()
    {
        // Arrange
        _mockMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(1, 1, 0, 1, 1)));

        _mockMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Memory store unavailable"));

        // Act
        var result = await _experienceReplay.TrainOnExperiencesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Training failed");
    }
}
