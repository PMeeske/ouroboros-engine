// <copyright file="SkillExtractorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class SkillExtractorTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<Ouroboros.Core.Ethics.IEthicsFramework> _ethicsMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new SkillExtractor(null!, _skillsMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ShouldExtractSkillAsync_UnverifiedResult_ReturnsFalse()
    {
        var extractor = CreateExtractor();
        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
        var verification = new PlanVerificationResult(exec, false, 0.3,
            new List<string>(), new List<string>(), DateTime.UtcNow);

        var result = await extractor.ShouldExtractSkillAsync(verification);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractSkillAsync_NullExecution_ReturnsFailure()
    {
        var extractor = CreateExtractor();
        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
        var verification = new PlanVerificationResult(exec, true, 0.9,
            new List<string>(), new List<string>(), DateTime.UtcNow);

        var result = await extractor.ExtractSkillAsync(null!, verification);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task ExtractSkillAsync_NullVerification_ReturnsFailure()
    {
        var extractor = CreateExtractor();
        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));

        var result = await extractor.ExtractSkillAsync(exec, null!);

        result.IsFailure.Should().BeTrue();
    }

    private SkillExtractor CreateExtractor()
    {
        return new SkillExtractor(_llmMock.Object, _skillsMock.Object, _ethicsMock.Object);
    }
}
