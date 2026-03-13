// <copyright file="GrammarCompilationExceptionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

public class GrammarCompilationExceptionTests
{
    [Fact]
    public void Constructor_WithDiagnostics_ShouldStoreAll()
    {
        // Arrange
        var diagnostics = new List<string>
        {
            "error CS0246: The type 'Foo' could not be found",
            "error CS1002: ; expected",
        };

        // Act
        var ex = new GrammarCompilationException(
            "Compilation failed",
            CompilationStage.RoslynCompilation,
            diagnostics);

        // Assert
        ex.Message.Should().Be("Compilation failed");
        ex.Stage.Should().Be(CompilationStage.RoslynCompilation);
        ex.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithInnerException_ShouldPreserve()
    {
        // Arrange
        var inner = new InvalidOperationException("ANTLR not found");

        // Act
        var ex = new GrammarCompilationException(
            "ANTLR tool failed",
            CompilationStage.AntlrCodeGeneration,
            inner);

        // Assert
        ex.Stage.Should().Be(CompilationStage.AntlrCodeGeneration);
        ex.InnerException.Should().Be(inner);
        ex.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void GrammarEvolutionException_ShouldCaptureAttempts()
    {
        // Arrange & Act
        var ex = new GrammarEvolutionException(
            "Could not converge",
            "IF-THEN-ELSE rule language",
            5);

        // Assert
        ex.Description.Should().Be("IF-THEN-ELSE rule language");
        ex.Attempts.Should().Be(5);
    }
}
