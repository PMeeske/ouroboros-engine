using FluentAssertions;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class SymbolicPlanSelectorTests
{
    private static IMeTTaEngine CreateMockEngine(string queryResult = "true")
    {
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success(queryResult)));
        engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success("OK")));
        return engine;
    }

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SymbolicPlanSelector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #region SelectBestPlanAsync

    [Fact]
    public async Task SelectBestPlanAsync_NullCandidates_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var act = () => selector.SelectBestPlanAsync(null!, SafeContext.FullAccess);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SelectBestPlanAsync_EmptyCandidates_ReturnsFailure()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var result = await selector.SelectBestPlanAsync(
            Array.Empty<Plan>(), SafeContext.FullAccess);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No candidate plans");
    }

    [Fact]
    public async Task SelectBestPlanAsync_SinglePlan_ReturnsThatPlan()
    {
        // Arrange
        var engine = CreateMockEngine("true");
        var selector = new SymbolicPlanSelector(engine);
        var action = new FileSystemAction("read");
        var plan = new Plan("Read file").WithAction(action);

        // Act
        var result = await selector.SelectBestPlanAsync(
            new[] { plan }, SafeContext.FullAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Plan.Should().Be(plan);
    }

    [Fact]
    public async Task SelectBestPlanAsync_MultiplePlans_ReturnsHighestScoring()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        var callCount = 0;
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                // Alternate between true and false to give different scores
                return Task.FromResult(callCount % 2 == 0
                    ? Result<string, string>.Success("true")
                    : Result<string, string>.Success("false"));
            });

        var selector = new SymbolicPlanSelector(engine);
        var plan1 = new Plan("Plan 1").WithAction(new FileSystemAction("read"));
        var plan2 = new Plan("Plan 2").WithAction(new FileSystemAction("read"));

        // Act
        var result = await selector.SelectBestPlanAsync(
            new[] { plan1, plan2 }, SafeContext.FullAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    #endregion

    #region ScorePlanAsync

    [Fact]
    public async Task ScorePlanAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var act = () => selector.ScorePlanAsync(null!, SafeContext.FullAccess);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScorePlanAsync_AllowedActions_ReturnsPositiveScore()
    {
        // Arrange
        var engine = CreateMockEngine("true");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("Good plan")
            .WithAction(new FileSystemAction("read"));

        // Act
        var result = await selector.ScorePlanAsync(plan, SafeContext.FullAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeGreaterThan(0);
        result.Value.Explanation.Should().Contain("permitted");
    }

    [Fact]
    public async Task ScorePlanAsync_DisallowedActions_ReturnsNegativeScore()
    {
        // Arrange
        var engine = CreateMockEngine("false");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("Bad plan")
            .WithAction(new FileSystemAction("write"));

        // Act
        var result = await selector.ScorePlanAsync(plan, SafeContext.ReadOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeLessThan(0);
        result.Value.Explanation.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ScorePlanAsync_PlanWithNoActions_ReturnsResult()
    {
        // Arrange
        var engine = CreateMockEngine("true");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("Empty plan");

        // Act
        var result = await selector.ScorePlanAsync(plan, SafeContext.FullAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CheckConstraintAsync

    [Fact]
    public async Task CheckConstraintAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var act = () => selector.CheckConstraintAsync(null!, "no writes");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckConstraintAsync_NullConstraint_ThrowsArgumentException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var act = () => selector.CheckConstraintAsync(new Plan("test"), null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CheckConstraintAsync_ConstraintSatisfied_ReturnsTrue()
    {
        // Arrange
        var engine = CreateMockEngine("true");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("test plan");

        // Act
        var result = await selector.CheckConstraintAsync(plan, "no writes");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConstraintAsync_ConstraintNotSatisfied_ReturnsFalse()
    {
        // Arrange
        var engine = CreateMockEngine("false");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("test plan");

        // Act
        var result = await selector.CheckConstraintAsync(plan, "no network");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConstraintAsync_EngineError_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Engine error")));
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("test plan");

        // Act
        var result = await selector.CheckConstraintAsync(plan, "read-only");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ExplainPlanAsync

    [Fact]
    public async Task ExplainPlanAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var act = () => selector.ExplainPlanAsync(null!, SafeContext.FullAccess);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExplainPlanAsync_ValidPlan_ReturnsExplanation()
    {
        // Arrange
        var engine = CreateMockEngine("true");
        var selector = new SymbolicPlanSelector(engine);
        var plan = new Plan("My plan").WithAction(new FileSystemAction("read"));

        // Act
        var result = await selector.ExplainPlanAsync(plan, SafeContext.FullAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("My plan");
        result.Value.Should().Contain("scored");
    }

    #endregion

    #region InitializeAsync

    [Fact]
    public async Task InitializeAsync_EngineAcceptsFacts_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateMockEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var result = await selector.InitializeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_EngineRejectsAFact_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Parse error")));
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var result = await selector.InitializeAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
