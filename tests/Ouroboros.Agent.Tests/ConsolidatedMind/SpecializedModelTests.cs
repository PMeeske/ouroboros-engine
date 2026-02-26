// <copyright file="SpecializedModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public class SpecializedModelTests
{
    private readonly Mock<IChatCompletionModel> _modelMock = new();

    [Fact]
    public void Constructor_SetsProperties()
    {
        var model = new SpecializedModel(
            SpecializedRole.CodeExpert,
            _modelMock.Object,
            "TestModel",
            new[] { "code", "debug" });

        model.Role.Should().Be(SpecializedRole.CodeExpert);
        model.ModelName.Should().Be("TestModel");
        model.Capabilities.Should().Contain("code");
        model.Priority.Should().Be(1.0);
        model.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void CalculateFitness_NullCapabilities_ReturnsDefault()
    {
        var model = CreateModel(new[] { "code", "debug" });

        var fitness = model.CalculateFitness(null!);

        fitness.Should().Be(0.5);
    }

    [Fact]
    public void CalculateFitness_EmptyCapabilities_ReturnsDefault()
    {
        var model = CreateModel(new[] { "code", "debug" });

        var fitness = model.CalculateFitness(Array.Empty<string>());

        fitness.Should().Be(0.5);
    }

    [Fact]
    public void CalculateFitness_PerfectMatch_ReturnsHighScore()
    {
        var model = CreateModel(new[] { "code", "debug" });

        var fitness = model.CalculateFitness(new[] { "code", "debug" });

        fitness.Should().Be(1.0);
    }

    [Fact]
    public void CalculateFitness_NoMatch_ReturnsZero()
    {
        var model = CreateModel(new[] { "code", "debug" });

        var fitness = model.CalculateFitness(new[] { "math", "physics" });

        fitness.Should().Be(0.0);
    }

    [Fact]
    public void CalculateFitness_PartialMatch_ReturnsProportional()
    {
        var model = CreateModel(new[] { "code", "debug" }, priority: 1.0);

        var fitness = model.CalculateFitness(new[] { "code", "math" });

        fitness.Should().Be(0.5);
    }

    [Fact]
    public void CalculateFitness_CaseInsensitive()
    {
        var model = CreateModel(new[] { "Code", "Debug" });

        var fitness = model.CalculateFitness(new[] { "code", "debug" });

        fitness.Should().Be(1.0);
    }

    private SpecializedModel CreateModel(string[] capabilities, double priority = 1.0)
    {
        return new SpecializedModel(
            SpecializedRole.CodeExpert,
            _modelMock.Object,
            "TestModel",
            capabilities,
            Priority: priority);
    }
}
