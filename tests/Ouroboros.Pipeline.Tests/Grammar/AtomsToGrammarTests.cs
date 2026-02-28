// <copyright file="AtomsToGrammarTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Moq;
using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

/// <summary>
/// Tests for the Logic Transfer Object (LTO) pipeline:
/// MeTTa atoms → wire → .g4 grammar → compilation.
/// </summary>
public class AtomsToGrammarTests
{
    private readonly Mock<IGrammarValidator> _validatorMock;

    public AtomsToGrammarTests()
    {
        _validatorMock = new Mock<IGrammarValidator>();
    }

    [Fact]
    public async Task AtomsToGrammarAsync_SimpleGrammar_ShouldReturnG4()
    {
        // Arrange
        string mettaAtoms = """
            (MkRegexTerminal "NUMBER" "[0-9]+")
            (MkTerminal "PLUS")
            (MkProduction "expr" (Cons (Cons "term" (Cons "exprPrime" Nil)) Nil))
            (MkProduction "exprPrime" (Cons (Cons "PLUS" (Cons "term" (Cons "exprPrime" Nil))) (Cons Nil Nil)))
            (MkProduction "term" (Cons (Cons "NUMBER" Nil) Nil))
            (MkGrammar "Arithmetic" "expr" (Cons (MkProduction "expr" ...) (Cons (MkProduction "exprPrime" ...) (Cons (MkProduction "term" ...) Nil))))
            """;

        string expectedG4 = "grammar Arithmetic;\n\nexpr\n    : term exprPrime\n    ;\n";

        _validatorMock.Setup(v => v.AtomsToGrammarAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, expectedG4, new List<string> { "Generated 3 production(s)" }.AsReadOnly()));

        // Act
        var (success, g4, notes) = await _validatorMock.Object.AtomsToGrammarAsync(mettaAtoms);

        // Assert
        success.Should().BeTrue();
        g4.Should().Contain("grammar Arithmetic");
        notes.Should().ContainSingle();
    }

    [Fact]
    public async Task ValidateAtomsAsync_ValidAtoms_ShouldReturnNoIssues()
    {
        // Arrange
        string mettaAtoms = """
            (MkProduction "expr" (Cons (Cons "NUMBER" Nil) Nil))
            (MkGrammar "Simple" "expr" (Cons (MkProduction "expr" ...) Nil))
            """;

        _validatorMock.Setup(v => v.ValidateAtomsAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new GrammarValidationResult(true, Array.Empty<GrammarIssue>()),
                new List<string>().AsReadOnly() as IReadOnlyList<string>));

        // Act
        var (result, notes) = await _validatorMock.Object.ValidateAtomsAsync(mettaAtoms);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAtomsAsync_LeftRecursiveAtoms_ShouldReturnIssue()
    {
        // Arrange: atom spec with left recursion (expr -> expr PLUS term)
        string mettaAtoms = """
            (MkProduction "expr" (Cons (Cons "expr" (Cons "PLUS" (Cons "term" Nil))) Nil))
            (MkGrammar "Bad" "expr" (Cons (MkProduction "expr" ...) Nil))
            """;

        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Direct left recursion in atom spec", GrammarIssueKind.LeftRecursion),
        };

        _validatorMock.Setup(v => v.ValidateAtomsAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new GrammarValidationResult(false, issues),
                new List<string> { "Structural validation found 1 issue" }.AsReadOnly() as IReadOnlyList<string>));

        // Act
        var (result, notes) = await _validatorMock.Object.ValidateAtomsAsync(mettaAtoms);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Kind.Should().Be(GrammarIssueKind.LeftRecursion);
    }

    [Fact]
    public async Task CorrectAtomsAsync_LeftRecursion_ShouldCorrectAndReturnFixedAtoms()
    {
        // Arrange
        string originalAtoms = """
            (MkProduction "expr" (Cons (Cons "expr" (Cons "PLUS" (Cons "term" Nil))) (Cons (Cons "term" Nil) Nil)))
            """;

        string correctedAtoms = """
            (MkProduction "expr" (Cons (Cons "term" (Cons "expr_prime" Nil)) Nil))
            (MkProduction "expr_prime" (Cons (Cons "PLUS" (Cons "term" (Cons "expr_prime" Nil))) (Cons Nil Nil)))
            """;

        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Left recursion", GrammarIssueKind.LeftRecursion),
        };

        _validatorMock.Setup(v => v.CorrectAtomsAsync(
                originalAtoms, issues, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                true,
                correctedAtoms,
                new List<string> { "Removed left recursion from 'expr' in atom spec" }.AsReadOnly() as IReadOnlyList<string>,
                Array.Empty<GrammarIssue>() as IReadOnlyList<GrammarIssue>));

        // Act
        var (success, corrected, corrections, remaining) =
            await _validatorMock.Object.CorrectAtomsAsync(originalAtoms, issues);

        // Assert
        success.Should().BeTrue();
        corrected.Should().Contain("expr_prime");
        corrections.Should().ContainSingle();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task FullLtoPipeline_AtomsValidateCorrectedConverted_ShouldSucceed()
    {
        // Arrange: simulate full round-trip atoms → validate → correct → convert
        string mettaAtoms = """
            (MkProduction "expr" (Cons (Cons "expr" (Cons "PLUS" (Cons "term" Nil))) (Cons (Cons "term" Nil) Nil)))
            (MkProduction "term" (Cons (Cons "NUMBER" Nil) Nil))
            (MkRegexTerminal "NUMBER" "[0-9]+")
            (MkTerminal "PLUS")
            (MkGrammar "Calc" "expr" ...)
            """;

        string correctedAtoms = """
            (MkProduction "expr" (Cons (Cons "term" (Cons "exprPrime" Nil)) Nil))
            (MkProduction "exprPrime" (Cons (Cons "PLUS" (Cons "term" (Cons "exprPrime" Nil))) (Cons Nil Nil)))
            (MkProduction "term" (Cons (Cons "NUMBER" Nil) Nil))
            (MkRegexTerminal "NUMBER" "[0-9]+")
            (MkTerminal "PLUS")
            (MkGrammar "Calc" "expr" ...)
            """;

        string finalG4 = """
            grammar Calc;

            expr
                : term exprPrime
                ;

            exprPrime
                : PLUS term exprPrime
                |
                ;

            term
                : NUMBER
                ;

            NUMBER: [0-9]+;
            PLUS: '+';
            WS: [ \t\r\n]+ -> skip;
            """;

        var leftRecursionIssue = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Left recursion", GrammarIssueKind.LeftRecursion),
        };

        // Step 1: ValidateAtoms finds left recursion
        _validatorMock.Setup(v => v.ValidateAtomsAsync(
                mettaAtoms, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new GrammarValidationResult(false, leftRecursionIssue),
                new List<string>().AsReadOnly() as IReadOnlyList<string>));

        // Step 2: CorrectAtoms fixes it
        _validatorMock.Setup(v => v.CorrectAtomsAsync(
                mettaAtoms, leftRecursionIssue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                true,
                correctedAtoms,
                new List<string> { "Fixed left recursion" }.AsReadOnly() as IReadOnlyList<string>,
                Array.Empty<GrammarIssue>() as IReadOnlyList<GrammarIssue>));

        // Step 3: AtomsToGrammar converts to .g4
        _validatorMock.Setup(v => v.AtomsToGrammarAsync(
                correctedAtoms, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, finalG4, new List<string>().AsReadOnly() as IReadOnlyList<string>));

        // Act — simulate the pipeline steps
        var (validResult, _) = await _validatorMock.Object.ValidateAtomsAsync(mettaAtoms);
        validResult.IsValid.Should().BeFalse();

        var (corrSuccess, corrected, _, _) =
            await _validatorMock.Object.CorrectAtomsAsync(mettaAtoms, validResult.Issues);
        corrSuccess.Should().BeTrue();

        var (convertSuccess, g4, _) = await _validatorMock.Object.AtomsToGrammarAsync(corrected);

        // Assert
        convertSuccess.Should().BeTrue();
        g4.Should().Contain("grammar Calc");
        g4.Should().Contain("exprPrime");
        g4.Should().NotContain("expr : expr"); // No left recursion in output

        // Verify the full pipeline was exercised
        _validatorMock.Verify(v => v.ValidateAtomsAsync(
            mettaAtoms, It.IsAny<CancellationToken>()), Times.Once);
        _validatorMock.Verify(v => v.CorrectAtomsAsync(
            mettaAtoms, leftRecursionIssue, It.IsAny<CancellationToken>()), Times.Once);
        _validatorMock.Verify(v => v.AtomsToGrammarAsync(
            correctedAtoms, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtomsToGrammarAsync_InvalidAtoms_ShouldReturnFailure()
    {
        // Arrange: malformed MeTTa
        string badAtoms = "this is not valid metta (((";

        _validatorMock.Setup(v => v.AtomsToGrammarAsync(
                badAtoms, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "", new List<string> { "Failed to parse MeTTa atoms" }.AsReadOnly() as IReadOnlyList<string>));

        // Act
        var (success, g4, notes) = await _validatorMock.Object.AtomsToGrammarAsync(badAtoms);

        // Assert
        success.Should().BeFalse();
        g4.Should().BeEmpty();
        notes.Should().Contain(n => n.Contains("Failed"));
    }

    [Fact]
    public async Task AtomsToGrammarAsync_NullInput_ShouldThrow()
    {
        // Arrange
        _validatorMock.Setup(v => v.AtomsToGrammarAsync(
                It.Is<string>(s => string.IsNullOrWhiteSpace(s)), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Value cannot be null or whitespace.", "mettaAtoms"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _validatorMock.Object.AtomsToGrammarAsync(null!));
    }
}
