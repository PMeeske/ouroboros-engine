// <copyright file="GrammarValidationStepTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Moq;
using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

public class GrammarValidationStepTests
{
    private readonly Mock<IGrammarValidator> _validatorMock;

    public GrammarValidationStepTests()
    {
        _validatorMock = new Mock<IGrammarValidator>();
    }

    [Fact]
    public void Constructor_NullValidator_ShouldThrow()
    {
        var act = () => new GrammarValidationStep(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("validator");
    }

    [Fact]
    public async Task ValidateAsync_ValidGrammar_ShouldReturnSuccess()
    {
        // Arrange
        _validatorMock.Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrammarValidationResult(true, Array.Empty<GrammarIssue>()));

        var step = new GrammarValidationStep(_validatorMock.Object);

        // Act
        var result = await step.ValidateAsync("grammar Test; rule : 'a';");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_InvalidGrammar_ShouldReturnIssues()
    {
        // Arrange
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Left recursion", GrammarIssueKind.LeftRecursion),
        };
        _validatorMock.Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrammarValidationResult(false, issues));

        var step = new GrammarValidationStep(_validatorMock.Object);

        // Act
        var result = await step.ValidateAsync("grammar Bad; expr : expr '+' term;");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Kind.Should().Be(GrammarIssueKind.LeftRecursion);
    }

    [Fact]
    public async Task ValidateAsync_NullGrammar_ShouldThrow()
    {
        // Arrange
        var step = new GrammarValidationStep(_validatorMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => step.ValidateAsync(null!));
    }

    [Fact]
    public async Task ValidateAndCorrectAsync_ValidGrammar_ShouldReturnUnchanged()
    {
        // Arrange
        string grammar = "grammar Valid; rule : 'a' | 'b';";
        _validatorMock.Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrammarValidationResult(true, Array.Empty<GrammarIssue>()));

        var step = new GrammarValidationStep(_validatorMock.Object);

        // Act
        var result = await step.ValidateAndCorrectAsync(grammar);

        // Assert
        result.Should().Be(grammar);
    }

    [Fact]
    public async Task ValidateAndCorrectAsync_InvalidGrammar_ShouldAttemptCorrection()
    {
        // Arrange
        string original = "grammar Bad; expr : expr '+' term;";
        string corrected = "grammar Bad; expr : term exprPrime; exprPrime : '+' term exprPrime | ;";
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Left recursion", GrammarIssueKind.LeftRecursion),
        };

        _validatorMock.Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrammarValidationResult(false, issues));

        _validatorMock.Setup(v => v.CorrectAsync(
                original, issues, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrammarCorrectionResult(
                true,
                corrected,
                new List<string> { "Removed left recursion from 'expr'" },
                Array.Empty<GrammarIssue>()));

        var step = new GrammarValidationStep(_validatorMock.Object);

        // Act
        var result = await step.ValidateAndCorrectAsync(original);

        // Assert
        result.Should().Be(corrected);
        _validatorMock.Verify(v => v.CorrectAsync(
            original, issues, It.IsAny<CancellationToken>()), Times.Once);
    }
}
