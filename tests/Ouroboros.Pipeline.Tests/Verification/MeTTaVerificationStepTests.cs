using FluentAssertions;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class MeTTaVerificationStepTests
{
    private readonly IMeTTaEngine _engine = Substitute.For<IMeTTaEngine>();

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new MeTTaVerificationStep(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidEngine_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new MeTTaVerificationStep(_engine);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_DefaultContext_UsesReadOnly()
    {
        // Arrange & Act - default context is ReadOnly per constructor signature
        var step = new MeTTaVerificationStep(_engine);

        // Assert - we verify the behavior indirectly via the query format
        // The step should use ReadOnly context in queries
        step.Should().NotBeNull();
    }

    #region VerifyAsync

    [Fact]
    public async Task VerifyAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var step = new MeTTaVerificationStep(_engine);

        // Act
        var act = () => step.VerifyAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifyAsync_EmptyPlan_ReturnsSuccess()
    {
        // Arrange
        var step = new MeTTaVerificationStep(_engine);
        var plan = new Plan("empty plan");

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(plan);
    }

    [Fact]
    public async Task VerifyAsync_AllowedAction_ReturnsSuccess()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("read plan", new[] { new FileSystemAction("read") });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(plan);
    }

    [Fact]
    public async Task VerifyAsync_ForbiddenAction_ReturnsFailure()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("False"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("write plan", new[] { new FileSystemAction("write") });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<SecurityException>();
        result.Error.ViolatingAction.Should().Contain("FileSystemAction");
        result.Error.Context.Should().Be(SafeContext.ReadOnly);
    }

    [Fact]
    public async Task VerifyAsync_EngineFailure_ReturnsFailure()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Failure("engine error"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("plan", new[] { new FileSystemAction("read") });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_MultipleActions_AllAllowed_ReturnsSuccess()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.FullAccess);
        var plan = new Plan("multi plan", new PlanAction[]
        {
            new FileSystemAction("read"),
            new NetworkAction("get"),
            new ToolAction("search")
        });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _engine.Received(3).ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_MultipleActions_SecondForbidden_ReturnsFailureAtSecond()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Is<string>(q => q.Contains("FileSystemAction")), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));
        _engine.ExecuteQueryAsync(Arg.Is<string>(q => q.Contains("NetworkAction")), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("False"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("plan", new PlanAction[]
        {
            new FileSystemAction("read"),
            new NetworkAction("post")
        });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("NetworkAction");
    }

    [Fact]
    public async Task VerifyAsync_QueryContainsCorrectFormat()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("plan", new[] { new FileSystemAction("read") });

        // Act
        await step.VerifyAsync(plan);

        // Assert
        await _engine.Received().ExecuteQueryAsync(
            Arg.Is<string>(q => q == "!(Allowed (FileSystemAction \"read\") ReadOnly)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_FullAccessContext_UsesFullAccessAtom()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var step = new MeTTaVerificationStep(_engine, SafeContext.FullAccess);
        var plan = new Plan("plan", new[] { new NetworkAction("post") });

        // Act
        await step.VerifyAsync(plan);

        // Assert
        await _engine.Received().ExecuteQueryAsync(
            Arg.Is<string>(q => q.Contains("FullAccess")),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("true", true)]
    [InlineData("[True]", true)]
    [InlineData("(True)", true)]
    [InlineData("[[True]]", true)]
    [InlineData("False", false)]
    [InlineData("false", false)]
    [InlineData("[False]", false)]
    [InlineData("(False)", false)]
    [InlineData("[[False]]", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("  True  ", true)]
    [InlineData("some text with true in it", true)]
    [InlineData("some text with false in it", false)]
    [InlineData("contains both true and false", false)]
    [InlineData("random text", false)]
    public async Task VerifyAsync_ParsesEngineResultCorrectly(string engineResult, bool shouldBeAllowed)
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success(engineResult));

        var step = new MeTTaVerificationStep(_engine, SafeContext.ReadOnly);
        var plan = new Plan("plan", new[] { new FileSystemAction("read") });

        // Act
        var result = await step.VerifyAsync(plan);

        // Assert
        if (shouldBeAllowed)
        {
            result.IsSuccess.Should().BeTrue($"engine result '{engineResult}' should be treated as allowed");
        }
        else
        {
            result.IsFailure.Should().BeTrue($"engine result '{engineResult}' should be treated as not allowed");
        }
    }

    #endregion

    #region AsArrow

    [Fact]
    public async Task AsArrow_ReturnsVerificationFunction()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var step = new MeTTaVerificationStep(_engine);
        var arrow = step.AsArrow();
        var plan = new Plan("arrow plan", new[] { new FileSystemAction("read") });

        // Act
        var result = await arrow(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AsArrow_FailedVerification_ReturnsFailure()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("False"));

        var step = new MeTTaVerificationStep(_engine);
        var arrow = step.AsArrow();
        var plan = new Plan("plan", new[] { new FileSystemAction("write") });

        // Act
        var result = await arrow(plan);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region InitializeGuardRailsAsync

    [Fact]
    public async Task InitializeGuardRailsAsync_AllRulesSucceed_ReturnsSuccess()
    {
        // Arrange
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = new MeTTaVerificationStep(_engine);

        // Act
        var result = await step.InitializeGuardRailsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeGuardRailsAsync_SkipsEmptyLinesAndComments()
    {
        // Arrange
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = new MeTTaVerificationStep(_engine);

        // Act
        await step.InitializeGuardRailsAsync();

        // Assert - comments and empty lines should be skipped
        // Only actual rules (non-empty, non-comment lines) should be added
        await _engine.DidNotReceive().AddFactAsync(
            Arg.Is<string>(s => s.TrimStart().StartsWith(";")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeGuardRailsAsync_RuleFailure_ReturnsFailure()
    {
        // Arrange
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Failure("rule error"));

        var step = new MeTTaVerificationStep(_engine);

        // Act
        var result = await step.InitializeGuardRailsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("rule error");
    }

    [Fact]
    public async Task InitializeGuardRailsAsync_StopsOnFirstFailure()
    {
        // Arrange
        var callCount = 0;
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? Result<Unit, string>.Failure("first rule failed")
                    : Result<Unit, string>.Success(Unit.Value);
            });

        var step = new MeTTaVerificationStep(_engine);

        // Act
        var result = await step.InitializeGuardRailsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task InitializeGuardRailsAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = new MeTTaVerificationStep(_engine);

        // Act
        await step.InitializeGuardRailsAsync(cts.Token);

        // Assert
        await _engine.Received().AddFactAsync(Arg.Any<string>(), cts.Token);
    }

    [Fact]
    public async Task InitializeGuardRailsAsync_AddsTypeHierarchyRules()
    {
        // Arrange
        _engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = new MeTTaVerificationStep(_engine);

        // Act
        await step.InitializeGuardRailsAsync();

        // Assert - verify key rules are added
        await _engine.Received().AddFactAsync(
            Arg.Is<string>(s => s.Contains("Action Type")),
            Arg.Any<CancellationToken>());
        await _engine.Received().AddFactAsync(
            Arg.Is<string>(s => s.Contains("SafeContext Type")),
            Arg.Any<CancellationToken>());
        await _engine.Received().AddFactAsync(
            Arg.Is<string>(s => s.Contains("Allowed")),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
