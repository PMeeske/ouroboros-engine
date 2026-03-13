// <copyright file="MemoryStoreTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MemoryStoreTests
{
    [Fact]
    public void Constructor_NoParams_DoesNotThrow()
    {
        var act = () => new MemoryStore();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StoreExperienceAsync_NullExperience_ReturnsFailure()
    {
        var store = new MemoryStore();

        var result = await store.StoreExperienceAsync(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task StoreExperienceAsync_ValidExperience_Succeeds()
    {
        var store = new MemoryStore();
        var experience = CreateExperience("exp-1");

        var result = await store.StoreExperienceAsync(experience);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StoreExperienceAsync_EmptyId_ReturnsFailure()
    {
        var store = new MemoryStore();
        var experience = CreateExperience("");

        var result = await store.StoreExperienceAsync(experience);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task QueryExperiencesAsync_EmptyStore_ReturnsEmptyList()
    {
        var store = new MemoryStore();
        var query = new MemoryQuery(ContextSimilarity: "test context");

        var result = await store.QueryExperiencesAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    private static Experience CreateExperience(string id)
    {
        var plan = new MetaAIPlan("goal", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
        var verification = new PlanVerificationResult(exec, true, 0.9,
            new List<string>(), new List<string>(), DateTime.UtcNow);

        return new Experience(
            id,
            DateTime.UtcNow,
            "test context",
            "test action",
            "test outcome",
            true,
            new List<string> { "test" },
            "test goal",
            exec,
            verification);
    }
}
