// <copyright file="AdaptivePlannerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptivePlannerTests
{
    private readonly Mock<IMetaAIPlannerOrchestrator> _orchestratorMock = new();
    private readonly Mock<IChatCompletionModel> _llmMock = new();

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        var act = () => new AdaptivePlanner(null!, _llmMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new AdaptivePlanner(_orchestratorMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new AdaptivePlanner(_orchestratorMock.Object, _llmMock.Object);
        act.Should().NotThrow();
    }
}
