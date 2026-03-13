// <copyright file="ParallelExecutorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ParallelExecutorTests
{
    private readonly Mock<ISafetyGuard> _safetyMock = new();

    [Fact]
    public void Constructor_NullSafety_Throws()
    {
        Func<MetaAIPlanStep, CancellationToken, Task<StepResult>> executor =
            (step, ct) => Task.FromResult(new StepResult(
                step, true, "ok", null, TimeSpan.FromMilliseconds(1),
                new Dictionary<string, object>()));

        var act = () => new ParallelExecutor(null!, executor);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullExecutor_Throws()
    {
        var act = () => new ParallelExecutor(_safetyMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        Func<MetaAIPlanStep, CancellationToken, Task<StepResult>> executor =
            (step, ct) => Task.FromResult(new StepResult(
                step, true, "ok", null, TimeSpan.FromMilliseconds(1),
                new Dictionary<string, object>()));

        var act = () => new ParallelExecutor(_safetyMock.Object, executor);
        act.Should().NotThrow();
    }
}
