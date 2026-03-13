// <copyright file="DynamicParserFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Pipeline.Grammar;

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory that compiles ANTLR4 .g4 grammars into working parsers at runtime
/// using the ANTLR tool for C# code generation and Roslyn for in-memory compilation.
/// </summary>
/// <remarks>
/// The compilation pipeline:
/// <list type="number">
/// <item>Write .g4 to a temp directory</item>
/// <item>Run the ANTLR tool to generate C# lexer + parser source</item>
/// <item>Compile the generated C# with Roslyn into an in-memory assembly</item>
/// <item>Load the assembly into a sandboxed <see cref="SandboxedCompilationContext"/></item>
/// <item>Instantiate the parser via reflection</item>
/// </list>
/// Each compilation uses a collectible <see cref="SandboxedCompilationContext"/>
/// so that compiled assemblies can be garbage-collected after use.
/// </remarks>
public sealed class DynamicParserFactory : IDisposable
{
    private static readonly Regex GrammarNameRegex = new(@"grammar\s+(\w+)\s*;", RegexOptions.Compiled);

    private readonly string _antlrToolPath;
    private readonly IReadOnlyList<MetadataReference> _references;
    private readonly ILogger<DynamicParserFactory>? _logger;
    private readonly List<SandboxedCompilationContext> _activeContexts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicParserFactory"/> class.
    /// </summary>
    /// <param name="antlrToolPath">
    /// Path to the ANTLR4 tool. Can be a Java JAR path or the .NET tool command.
    /// If null, defaults to "antlr4" (assumes the .NET global tool is installed).
    /// </param>
    /// <param name="additionalReferences">
    /// Additional assembly references for Roslyn compilation beyond the defaults.
    /// </param>
    /// <param name="logger">Optional logger.</param>
    public DynamicParserFactory(
        string? antlrToolPath = null,
        IEnumerable<MetadataReference>? additionalReferences = null,
        ILogger<DynamicParserFactory>? logger = null)
    {
        _antlrToolPath = antlrToolPath ?? "antlr4";
        _logger = logger;
        _references = BuildReferences(additionalReferences);
    }

    /// <summary>
    /// Gets the number of currently active compilation contexts.
    /// </summary>
    public int ActiveContextCount
    {
        get
        {
            lock (_activeContexts)
            {
                return _activeContexts.Count;
            }
        }
    }

    /// <summary>
    /// Compiles an ANTLR4 grammar string into an in-memory assembly.
    /// </summary>
    /// <param name="grammarG4">The full ANTLR4 grammar (.g4 content).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A compiled parser result containing the assembly and metadata.</returns>
    /// <exception cref="GrammarCompilationException">Thrown when compilation fails at any stage.</exception>
    public async Task<CompiledGrammar> CompileGrammarAsync(string grammarG4, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grammarG4);

        string grammarName = ExtractGrammarName(grammarG4);
        string tempDir = CreateTempDirectory(grammarName);

        try
        {
            // Step 1: Write .g4 to temp directory
            string grammarPath = Path.Combine(tempDir, $"{grammarName}.g4");
            await File.WriteAllTextAsync(grammarPath, grammarG4, ct);

            // Step 2: Run ANTLR tool to generate C# source
            await RunAntlrToolAsync(grammarPath, tempDir, ct);

            // Step 3: Compile generated C# with Roslyn
            var csFiles = Directory.GetFiles(tempDir, "*.cs");
            if (csFiles.Length == 0)
            {
                throw new GrammarCompilationException(
                    "ANTLR tool produced no C# files",
                    CompilationStage.AntlrCodeGeneration);
            }

            var assembly = await CompileWithRoslynAsync(csFiles, grammarName, ct);

            _logger?.LogInformation(
                "Grammar '{GrammarName}' compiled successfully ({FileCount} source files)",
                grammarName,
                csFiles.Length);

            return assembly;
        }
        finally
        {
            // Clean up temp directory
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clean up temp directory {TempDir}", tempDir);
            }
        }
    }

    /// <summary>
    /// Releases a compiled grammar's resources, allowing the assembly to be garbage collected.
    /// </summary>
    /// <param name="compiledGrammar">The compiled grammar to release.</param>
    public void ReleaseGrammar(CompiledGrammar compiledGrammar)
    {
        lock (_activeContexts)
        {
            _activeContexts.Remove(compiledGrammar.Context);
        }

        compiledGrammar.Context.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_activeContexts)
        {
            foreach (var context in _activeContexts)
            {
                context.Dispose();
            }

            _activeContexts.Clear();
        }
    }

    private static string ExtractGrammarName(string grammarG4)
    {
        var match = GrammarNameRegex.Match(grammarG4);
        return match.Success ? match.Groups[1].Value : "DynamicGrammar";
    }

    private static string CreateTempDirectory(string grammarName)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ouroboros-grammar", $"{grammarName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private async Task RunAntlrToolAsync(string grammarPath, string workingDir, CancellationToken ct)
    {
        string fileName;

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (_antlrToolPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            // Java JAR mode
            fileName = "java";
            psi.FileName = fileName;
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(_antlrToolPath);
        }
        else
        {
            // .NET tool mode (antlr4 global tool or dotnet-antlr)
            fileName = _antlrToolPath;
            psi.FileName = fileName;
        }

        psi.ArgumentList.Add("-Dlanguage=CSharp");
        psi.ArgumentList.Add("-visitor");
        psi.ArgumentList.Add("-no-listener");
        psi.ArgumentList.Add(grammarPath);

        // SECURITY: validated — ArgumentList prevents injection from antlrToolPath
        // and grammarPath (temp file). UseShellExecute = false prevents shell interpretation.
        using var process = Process.Start(psi)
            ?? throw new GrammarCompilationException(
                $"Failed to start ANTLR tool: {fileName}",
                CompilationStage.AntlrCodeGeneration);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            string errors = await process.StandardError.ReadToEndAsync(ct);
            throw new GrammarCompilationException(
                $"ANTLR code generation failed: {errors}",
                CompilationStage.AntlrCodeGeneration,
                [errors]);
        }
    }

    private async Task<CompiledGrammar> CompileWithRoslynAsync(
        string[] csFiles,
        string grammarName,
        CancellationToken ct)
    {
        var syntaxTrees = new List<SyntaxTree>(csFiles.Length);
        foreach (var file in csFiles)
        {
            string source = await File.ReadAllTextAsync(file, ct);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(source, path: file));
        }

        var compilation = CSharpCompilation.Create(
            $"DynamicParser_{grammarName}_{Guid.NewGuid():N}",
            syntaxTrees,
            _references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();

            throw new GrammarCompilationException(
                $"Roslyn compilation failed with {errors.Count} error(s)",
                CompilationStage.RoslynCompilation,
                errors);
        }

        // Load into sandboxed context
        var context = new SandboxedCompilationContext($"Grammar_{grammarName}");
        var assembly = context.LoadFromMemoryStream(ms);

        lock (_activeContexts)
        {
            _activeContexts.Add(context);
        }

        // Discover parser and lexer types
        var lexerType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name.EndsWith("Lexer") && t.BaseType?.Name == "Lexer");
        var parserType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name.EndsWith("Parser") && t.BaseType?.Name == "Parser");

        if (parserType == null)
        {
            throw new GrammarCompilationException(
                $"No Parser type found in compiled assembly for grammar '{grammarName}'",
                CompilationStage.ParserInstantiation);
        }

        return new CompiledGrammar(
            GrammarName: grammarName,
            Assembly: assembly,
            Context: context,
            ParserType: parserType,
            LexerType: lexerType);
    }

    private static IReadOnlyList<MetadataReference> BuildReferences(
        IEnumerable<MetadataReference>? additionalReferences)
    {
        var references = new List<MetadataReference>();

        // Core runtime references
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Linq.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Console.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.IO.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")));

        // ANTLR4 runtime — resolve from the app's loaded assemblies
        var antlrAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Antlr4.Runtime.Standard");
        if (antlrAssembly != null && !string.IsNullOrEmpty(antlrAssembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(antlrAssembly.Location));
        }

        if (additionalReferences != null)
        {
            references.AddRange(additionalReferences);
        }

        return references;
    }
}

/// <summary>
/// Represents a successfully compiled ANTLR grammar with its assembly and metadata.
/// </summary>
/// <param name="GrammarName">The grammar name extracted from the .g4 source.</param>
/// <param name="Assembly">The compiled in-memory assembly.</param>
/// <param name="Context">The sandboxed load context (dispose to free memory).</param>
/// <param name="ParserType">The generated Parser type.</param>
/// <param name="LexerType">The generated Lexer type, if available.</param>
public sealed record CompiledGrammar(
    string GrammarName,
    Assembly Assembly,
    SandboxedCompilationContext Context,
    Type ParserType,
    Type? LexerType);
