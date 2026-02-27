// <copyright file="GrammarCompilationException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Exception thrown when grammar compilation fails at any stage
/// (ANTLR code generation or Roslyn compilation).
/// </summary>
public sealed class GrammarCompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GrammarCompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="stage">The compilation stage that failed.</param>
    /// <param name="diagnostics">Compiler diagnostics, if available.</param>
    public GrammarCompilationException(
        string message,
        CompilationStage stage,
        IReadOnlyList<string>? diagnostics = null)
        : base(message)
    {
        Stage = stage;
        Diagnostics = diagnostics ?? Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrammarCompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="stage">The compilation stage that failed.</param>
    /// <param name="innerException">The inner exception.</param>
    public GrammarCompilationException(string message, CompilationStage stage, Exception innerException)
        : base(message, innerException)
    {
        Stage = stage;
        Diagnostics = Array.Empty<string>();
    }

    /// <summary>
    /// Gets the compilation stage that failed.
    /// </summary>
    public CompilationStage Stage { get; }

    /// <summary>
    /// Gets the compiler diagnostics.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }
}

/// <summary>
/// Stages of the grammar compilation pipeline.
/// </summary>
public enum CompilationStage
{
    /// <summary>ANTLR tool generating C# from .g4.</summary>
    AntlrCodeGeneration,

    /// <summary>Roslyn compiling generated C# to assembly.</summary>
    RoslynCompilation,

    /// <summary>Loading the compiled assembly into the runtime.</summary>
    AssemblyLoading,

    /// <summary>Instantiating the parser from the compiled assembly.</summary>
    ParserInstantiation,
}
