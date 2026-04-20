// <copyright file="CollectiveMindGoalIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CollectiveMindGoalIntegrationTests
{
    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullMind_Throws()
    {
        var act = () => new CollectiveMindGoalIntegration(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithMindOnly_DoesNotThrow()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();

        var act = () => new CollectiveMindGoalIntegration(mind);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithMindAndHierarchy_DoesNotThrow()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var hierarchyMock = new Mock<IGoalHierarchy>();

        var act = () => new CollectiveMindGoalIntegration(mind, hierarchyMock.Object);

        act.Should().NotThrow();
    }

    // ── ExecutePipelineGoalAsync ───────────────────────────────────

    [Fact]
    public async Task ExecutePipelineGoalAsync_NullGoal_Throws()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);

        var act = () => sut.ExecutePipelineGoalAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── ExecuteAgentGoalAsync ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAgentGoalAsync_NullGoal_Throws()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);

        var act = () => sut.ExecuteAgentGoalAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── ExecuteHierarchicalPipelineGoalAsync ───────────────────────

    [Fact]
    public async Task ExecuteHierarchicalPipelineGoalAsync_NullGoal_Throws()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);

        var act = () => sut.ExecuteHierarchicalPipelineGoalAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── ExecuteSubGoalsAsync ───────────────────────────────────────

    [Fact]
    public async Task ExecuteSubGoalsAsync_NullSubGoals_Throws()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);

        var act = () => sut.ExecuteSubGoalsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteSubGoalsAsync_EmptySubGoals_ReturnsEmptyResults()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);
        var emptyGoals = Array.Empty<SubGoal>();

        var result = await sut.ExecuteSubGoalsAsync(emptyGoals);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteSubGoalsAsync_EmptySubGoals_SequentialExecution_ReturnsEmptyResults()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var sut = new CollectiveMindGoalIntegration(mind);
        var emptyGoals = Array.Empty<SubGoal>();

        var result = await sut.ExecuteSubGoalsAsync(emptyGoals, parallelExecution: false);

        result.Should().BeEmpty();
    }
}
