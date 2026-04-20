using FluentAssertions;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class MeTTaVerificationExtensionsTests
{
    private readonly IMeTTaEngine _engine = Substitute.For<IMeTTaEngine>();

    [Fact]
    public async Task WithVerification_AllowedPlan_ReturnsSuccess()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var plan = new Plan("test plan", new[] { new FileSystemAction("read") });
        Step<string, Plan> planStep = _ => Task.FromResult(plan);

        // Act
        var verifiedStep = planStep.WithVerification(_engine, SafeContext.ReadOnly);
        var result = await verifiedStep("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(plan);
    }

    [Fact]
    public async Task WithVerification_ForbiddenPlan_ReturnsFailure()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("False"));

        var plan = new Plan("test plan", new[] { new FileSystemAction("write") });
        Step<string, Plan> planStep = _ => Task.FromResult(plan);

        // Act
        var verifiedStep = planStep.WithVerification(_engine, SafeContext.ReadOnly);
        var result = await verifiedStep("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<SecurityException>();
    }

    [Fact]
    public async Task WithVerification_DefaultContext_UsesReadOnly()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var plan = new Plan("plan", new[] { new FileSystemAction("read") });
        Step<string, Plan> planStep = _ => Task.FromResult(plan);

        // Act
        var verifiedStep = planStep.WithVerification(_engine);
        await verifiedStep("input");

        // Assert
        await _engine.Received().ExecuteQueryAsync(
            Arg.Is<string>(q => q.Contains("ReadOnly")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithVerification_FullAccessContext_UsesFullAccess()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        var plan = new Plan("plan", new[] { new NetworkAction("post") });
        Step<int, Plan> planStep = _ => Task.FromResult(plan);

        // Act
        var verifiedStep = planStep.WithVerification(_engine, SafeContext.FullAccess);
        await verifiedStep(42);

        // Assert
        await _engine.Received().ExecuteQueryAsync(
            Arg.Is<string>(q => q.Contains("FullAccess")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithVerification_EmptyPlan_ReturnsSuccess()
    {
        // Arrange
        var plan = new Plan("empty");
        Step<string, Plan> planStep = _ => Task.FromResult(plan);

        // Act
        var verifiedStep = planStep.WithVerification(_engine);
        var result = await verifiedStep("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task WithVerification_PassesInputToPlanStep()
    {
        // Arrange
        _engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));

        string? capturedInput = null;
        var plan = new Plan("plan", new[] { new FileSystemAction("read") });
        Step<string, Plan> planStep = input =>
        {
            capturedInput = input;
            return Task.FromResult(plan);
        };

        // Act
        var verifiedStep = planStep.WithVerification(_engine);
        await verifiedStep("hello world");

        // Assert
        capturedInput.Should().Be("hello world");
    }
}
