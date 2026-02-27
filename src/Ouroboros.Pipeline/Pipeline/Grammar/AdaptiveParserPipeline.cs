// <copyright file="AdaptiveParserPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this

namespace Ouroboros.Pipeline.Grammar;

using LangChain.Providers;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates the full grammar evolution pipeline: LLM generation → MeTTa validation
/// → ANTLR compilation → parse attempt → feedback loop on failure.
/// </summary>
/// <remarks>
/// This is the main entry point for the dynamic parser system. Given a natural language
/// description and sample input, it produces a working parser through an iterative
/// refinement process:
/// <list type="number">
/// <item>Check for a previously proven grammar matching the description.</item>
/// <item>If none found, generate candidates via Ollama (LLM).</item>
/// <item>Validate and correct grammar through MeTTa (Hyperon sidecar).</item>
/// <item>Compile grammar via ANTLR + Roslyn.</item>
/// <item>Attempt to parse the input.</item>
/// <item>On parse failure, feed error back to MeTTa for grammar refinement.</item>
/// <item>Repeat until success or max attempts exhausted.</item>
/// </list>
/// </remarks>
public sealed class AdaptiveParserPipeline : IDisposable
{
    private readonly IChatModel _llm;
    private readonly IGrammarValidator _validator;
    private readonly DynamicParserFactory _compilerFactory;
    private readonly ILogger<AdaptiveParserPipeline>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveParserPipeline"/> class.
    /// </summary>
    /// <param name="llm">The LLM provider (typically Ollama) for grammar generation.</param>
    /// <param name="validator">The grammar validation service (Hyperon sidecar).</param>
    /// <param name="compilerFactory">The dynamic parser compiler.</param>
    /// <param name="logger">Optional logger.</param>
    public AdaptiveParserPipeline(
        IChatModel llm,
        IGrammarValidator validator,
        DynamicParserFactory compilerFactory,
        ILogger<AdaptiveParserPipeline>? logger = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _compilerFactory = compilerFactory ?? throw new ArgumentNullException(nameof(compilerFactory));
        _logger = logger;
    }

    /// <summary>
    /// Generates, validates, compiles, and returns a working parser for the given description.
    /// The pipeline adaptively refines the grammar on parse failures.
    /// </summary>
    /// <param name="description">Natural language description of the desired grammar.</param>
    /// <param name="sampleInput">A sample input to validate the parser against.</param>
    /// <param name="maxAttempts">Maximum number of evolution iterations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A compiled grammar ready for use.</returns>
    /// <exception cref="GrammarEvolutionException">
    /// Thrown when the grammar fails to converge after <paramref name="maxAttempts"/>.
    /// </exception>
    public async Task<CompiledGrammar> EvolveGrammarAsync(
        string description,
        string? sampleInput = null,
        int maxAttempts = 5,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        // Step 1: Try to retrieve a previously proven grammar
        var (found, existingG4, grammarId, score) = await _validator.RetrieveGrammarAsync(description, ct);
        if (found && score > 0.5)
        {
            _logger?.LogInformation(
                "Found proven grammar '{GrammarId}' with similarity {Score:F2}",
                grammarId,
                score);
            try
            {
                return await _compilerFactory.CompileGrammarAsync(existingG4, ct);
            }
            catch (GrammarCompilationException ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Cached grammar '{GrammarId}' failed to compile, falling through to generation",
                    grammarId);
            }
        }

        string? currentGrammar = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            _logger?.LogInformation(
                "Grammar evolution attempt {Attempt}/{MaxAttempts} for '{Description}'",
                attempt,
                maxAttempts,
                description);

            // Step 2: Generate grammar via LLM
            if (currentGrammar == null)
            {
                currentGrammar = await GenerateGrammarAsync(description, sampleInput, ct);
            }

            // Step 3: Validate via Hyperon sidecar
            var validation = await _validator.ValidateAsync(currentGrammar, ct);
            if (!validation.IsValid)
            {
                _logger?.LogInformation(
                    "Grammar has {IssueCount} issue(s), attempting correction",
                    validation.Issues.Count);

                var correction = await _validator.CorrectAsync(currentGrammar, validation.Issues, ct);
                if (correction.Success)
                {
                    currentGrammar = correction.CorrectedGrammarG4;
                    foreach (var c in correction.CorrectionsApplied)
                    {
                        _logger?.LogInformation("Applied correction: {Correction}", c);
                    }
                }
            }

            // Step 4: Compile
            CompiledGrammar compiled;
            try
            {
                compiled = await _compilerFactory.CompileGrammarAsync(currentGrammar, ct);
            }
            catch (GrammarCompilationException ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Compilation failed at stage {Stage}, regenerating grammar",
                    ex.Stage);
                currentGrammar = null; // Force regeneration
                continue;
            }

            // Step 5: If we have sample input, try to parse it
            if (!string.IsNullOrEmpty(sampleInput))
            {
                var parseResult = TryParse(compiled, sampleInput);
                if (!parseResult.Success)
                {
                    _logger?.LogInformation(
                        "Parse failed: {Error}. Refining grammar.",
                        parseResult.Error);

                    _compilerFactory.ReleaseGrammar(compiled);

                    // Feed failure back to MeTTa for refinement
                    var refinement = await _validator.RefineAsync(
                        currentGrammar,
                        parseResult.Failure!,
                        ct);

                    if (refinement.Success)
                    {
                        currentGrammar = refinement.RefinedGrammarG4;
                        _logger?.LogInformation("Refinement: {Explanation}", refinement.Explanation);
                    }
                    else
                    {
                        currentGrammar = null; // Force full regeneration
                    }

                    continue;
                }
            }

            // Step 6: Success — store as proven grammar
            _logger?.LogInformation("Grammar '{Name}' evolved successfully in {Attempts} attempt(s)", compiled.GrammarName, attempt);

            if (!string.IsNullOrEmpty(sampleInput))
            {
                await _validator.StoreProvenGrammarAsync(
                    description,
                    currentGrammar,
                    [sampleInput],
                    ct);
            }

            return compiled;
        }

        throw new GrammarEvolutionException(
            $"Could not converge on valid grammar after {maxAttempts} attempts",
            description,
            maxAttempts);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _compilerFactory.Dispose();
    }

    private async Task<string> GenerateGrammarAsync(
        string description,
        string? sampleInput,
        CancellationToken ct)
    {
        string prompt = BuildGenerationPrompt(description, sampleInput);

        var response = await _llm.GenerateAsync(prompt, cancellationToken: ct);
        string generated = response.ToString();

        // Extract .g4 content from LLM response (may be wrapped in markdown code blocks)
        return ExtractG4FromResponse(generated);
    }

    private static string BuildGenerationPrompt(string description, string? sampleInput)
    {
        var prompt = $"""
            Generate a complete ANTLR4 grammar (.g4 format) for the following language:

            {description}

            Requirements:
            - The grammar must be a single, complete .g4 file
            - Start with "grammar <Name>;" declaration
            - Include both parser rules (lowercase) and lexer rules (UPPERCASE)
            - Handle whitespace appropriately (typically skip WS)
            - Avoid left recursion where possible
            - Use clear, descriptive rule names
            """;

        if (!string.IsNullOrEmpty(sampleInput))
        {
            prompt += $"""

                The grammar must be able to parse this sample input:
                ```
                {sampleInput}
                ```
                """;
        }

        prompt += "\n\nRespond with ONLY the .g4 grammar content, no explanations.";

        return prompt;
    }

    private static string ExtractG4FromResponse(string response)
    {
        // Try to extract from markdown code block
        var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"```(?:antlr|g4|antlr4)?\s*\n(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // Try to find grammar declaration directly
        var grammarMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"(grammar\s+\w+\s*;.*)",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (grammarMatch.Success)
        {
            return grammarMatch.Groups[1].Value.Trim();
        }

        // Return as-is, hope for the best
        return response.Trim();
    }

    private static ParseAttemptResult TryParse(CompiledGrammar compiled, string input)
    {
        try
        {
            if (compiled.LexerType == null)
            {
                return new ParseAttemptResult(
                    false,
                    "No lexer type found in compiled grammar",
                    new ParseFailureInfo("", "", 0, 0, input));
            }

            // Create lexer: new XLexer(new AntlrInputStream(input))
            var inputStreamType = compiled.LexerType.Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "AntlrInputStream")
                ?? typeof(object).Assembly.GetTypes().FirstOrDefault(t => t.Name == "AntlrInputStream");

            // Use reflection to instantiate the ANTLR pipeline
            // This works with any generated grammar without knowing types at compile time
            var antlrInputStreamType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == "Antlr4.Runtime.AntlrInputStream");

            if (antlrInputStreamType == null)
            {
                return new ParseAttemptResult(
                    false,
                    "ANTLR runtime not available",
                    new ParseFailureInfo("", "", 0, 0, input));
            }

            var inputStream = Activator.CreateInstance(antlrInputStreamType, input);
            var lexer = Activator.CreateInstance(compiled.LexerType, inputStream);

            var commonTokenStreamType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == "Antlr4.Runtime.CommonTokenStream");

            if (commonTokenStreamType == null)
            {
                return new ParseAttemptResult(
                    false,
                    "ANTLR CommonTokenStream not available",
                    new ParseFailureInfo("", "", 0, 0, input));
            }

            var tokenStream = Activator.CreateInstance(commonTokenStreamType, lexer);
            var parser = Activator.CreateInstance(compiled.ParserType, tokenStream);

            // Find the first parser rule method (the entry rule)
            var entryRule = compiled.ParserType.GetMethods()
                .Where(m => m.DeclaringType == compiled.ParserType)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType.Name.EndsWith("Context"))
                .FirstOrDefault();

            if (entryRule == null)
            {
                return new ParseAttemptResult(
                    false,
                    "No entry rule found in parser",
                    new ParseFailureInfo("", "", 0, 0, input));
            }

            // Invoke the parser
            entryRule.Invoke(parser, null);

            // Check for syntax errors via NumberOfSyntaxErrors property
            var syntaxErrorsProp = compiled.ParserType.GetProperty("NumberOfSyntaxErrors");
            if (syntaxErrorsProp != null)
            {
                int errorCount = (int)(syntaxErrorsProp.GetValue(parser) ?? 0);
                if (errorCount > 0)
                {
                    return new ParseAttemptResult(
                        false,
                        $"Parse completed with {errorCount} syntax error(s)",
                        new ParseFailureInfo("unknown", "unknown", 0, 0, input));
                }
            }

            return new ParseAttemptResult(true, null, null);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            return new ParseAttemptResult(
                false,
                ex.InnerException.Message,
                new ParseFailureInfo("unknown", "unknown", 0, 0, input));
        }
        catch (Exception ex)
        {
            return new ParseAttemptResult(
                false,
                ex.Message,
                new ParseFailureInfo("unknown", "unknown", 0, 0, input));
        }
    }

    private sealed record ParseAttemptResult(bool Success, string? Error, ParseFailureInfo? Failure);
}
