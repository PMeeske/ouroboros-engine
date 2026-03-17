// <copyright file="CollectiveMindGoalIntegrationFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CollectiveMindGoalIntegrationFactoryTests
{
    // ── CreateWithHierarchy ────────────────────────────────────────

    [Fact]
    public void CreateWithHierarchy_ReturnsInstance()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();
        var hierarchyMock = new Mock<IGoalHierarchy>();

        var result = CollectiveMindGoalIntegrationFactory.CreateWithHierarchy(mind, hierarchyMock.Object);

        result.Should().NotBeNull();
        result.Should().BeOfType<CollectiveMindGoalIntegration>();
    }

    // ── CreateStandalone ───────────────────────────────────────────

    [Fact]
    public void CreateStandalone_ReturnsInstance()
    {
        var mind = CollectiveMindFactory.CreateDecomposed();

        var result = CollectiveMindGoalIntegrationFactory.CreateStandalone(mind);

        result.Should().NotBeNull();
        result.Should().BeOfType<CollectiveMindGoalIntegration>();
    }

    // ── CreateDecomposed ───────────────────────────────────────────

    [Fact]
    public void CreateDecomposed_WithNullSettings_ReturnsInstance()
    {
        var result = CollectiveMindGoalIntegrationFactory.CreateDecomposed();

        result.Should().NotBeNull();
        result.Should().BeOfType<CollectiveMindGoalIntegration>();
    }

    // ── CreateLocalFirst ───────────────────────────────────────────

    [Fact]
    public void CreateLocalFirst_WithDefaults_ReturnsInstance()
    {
        var result = CollectiveMindGoalIntegrationFactory.CreateLocalFirst();

        result.Should().NotBeNull();
        result.Should().BeOfType<CollectiveMindGoalIntegration>();
    }

    [Fact]
    public void CreateLocalFirst_WithCustomModel_ReturnsInstance()
    {
        var result = CollectiveMindGoalIntegrationFactory.CreateLocalFirst("custom-model");

        result.Should().NotBeNull();
        result.Should().BeOfType<CollectiveMindGoalIntegration>();
    }
}
