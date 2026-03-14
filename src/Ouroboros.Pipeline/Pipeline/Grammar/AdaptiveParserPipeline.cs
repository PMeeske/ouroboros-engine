// <copyright file="AdaptiveParserPipeline.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Pipeline.Grammar;

using LangChain.Providers;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates the full grammar evolution pipeline using Logic Transfer Objects (LTOs):
/// LLM → MeTTa spec atoms → wire → Hyperon validates+converts → .g4 → ANTLR+Roslyn → working parser.
/// </summary>
/// <remarks>
/// This is the main entry point for the dynamic parser system. The LLM generates formal
/// MeTTa atom specifications (Logic Transfer Objects) rather than raw .g4 text. These atoms
/// carry verifiable logic over the gRPC wire to the Hyperon sidecar, which validates,
/// corrects, and deterministically converts them to ANTLR grammars.
/// <list type="number">
/// <item>Check for a previously proven grammar matching the description.</item>
/// <item>If none found, instruct LLM to generate MeTTa grammar spec atoms.</item>
/// <item>Send atoms over wire → ValidateAtoms → CorrectAtoms → AtomsToGrammar.</item>
/// <item>Compile resulting .g4 via ANTLR + Roslyn.</item>
/// <item>Attempt to parse the input.</item>
/// <item>On parse failure, feed error back as atoms for refinement.</item>
/// <item>Repeat until success or max attempts exhausted.</item>
/// </list>
/// </remarks>
public sealed partial class AdaptiveParserPipeline : IDisposable
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
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        ArgumentNullException.ThrowIfNull(compilerFactory);
        _compilerFactory = compilerFactory;
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
#pragma warning disable CA1510 // Use ArgumentNullException.ThrowIfNull — must throw ArgumentException for null per API contract
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(description));
        }
#pragma warning restore CA1510

        // Step 1: Try to retrieve a previously proven grammar
        var (found, existingG4, grammarId, score) = await _validator.RetrieveGrammarAsync(description, ct).ConfigureAwait(false);
        if (found && score > 0.5)
        {
            _logger?.LogInformation(
                "Found proven grammar '{GrammarId}' with similarity {Score:F2}",
                grammarId,
                score);
            try
            {
                return await _compilerFactory.CompileGrammarAsync(existingG4, ct).ConfigureAwait(false);
            }
            catch (GrammarCompilationException ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Cached grammar '{GrammarId}' failed to compile, falling through to generation",
                    grammarId);
            }
        }

        string? currentMeTTaAtoms = null;
        string? currentGrammar;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            _logger?.LogInformation(
                "Grammar evolution attempt {Attempt}/{MaxAttempts} for '{Description}'",
                attempt,
                maxAttempts,
                description);

            // Step 2: Generate MeTTa grammar spec atoms via LLM (Logic Transfer Objects)
            if (currentMeTTaAtoms == null)
            {
                currentMeTTaAtoms = await GenerateMeTTaAtomsAsync(description, sampleInput, ct).ConfigureAwait(false);
            }

            // Step 3: Validate atoms via Hyperon sidecar
            var (atomValidation, validationNotes) = await _validator.ValidateAtomsAsync(currentMeTTaAtoms, ct).ConfigureAwait(false);
            foreach (var note in validationNotes)
            {
                _logger?.LogDebug("Atom validation note: {Note}", note);
            }

            if (!atomValidation.IsValid)
            {
                _logger?.LogInformation(
                    "Atom spec has {IssueCount} issue(s), attempting correction",
                    atomValidation.Issues.Count);

                var (corrSuccess, correctedAtoms, corrections, _) =
                    await _validator.CorrectAtomsAsync(currentMeTTaAtoms, atomValidation.Issues, ct).ConfigureAwait(false);

                if (corrSuccess)
                {
                    currentMeTTaAtoms = correctedAtoms;
                    foreach (var c in corrections)
                    {
                        _logger?.LogInformation("Atom correction: {Correction}", c);
                    }
                }
            }

            // Step 3b: Convert validated atoms to .g4 grammar over the wire
            var (convertSuccess, g4Grammar, convertNotes) =
                await _validator.AtomsToGrammarAsync(currentMeTTaAtoms, ct).ConfigureAwait(false);

            foreach (var note in convertNotes)
            {
                _logger?.LogDebug("Atoms→.g4 note: {Note}", note);
            }

            if (!convertSuccess || string.IsNullOrWhiteSpace(g4Grammar))
            {
                _logger?.LogWarning("Atom-to-grammar conversion failed, regenerating atoms");
                currentMeTTaAtoms = null;
                continue;
            }

            currentGrammar = g4Grammar;

            // Step 4: Compile .g4 via ANTLR + Roslyn
            CompiledGrammar compiled;
            try
            {
                compiled = await _compilerFactory.CompileGrammarAsync(currentGrammar, ct).ConfigureAwait(false);
            }
            catch (GrammarCompilationException ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Compilation failed at stage {Stage}, regenerating atoms",
                    ex.Stage);
                currentMeTTaAtoms = null;
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

                    // Feed failure back to MeTTa for .g4-level refinement
                    var refinement = await _validator.RefineAsync(
                        currentGrammar,
                        parseResult.Failure!,
                        ct).ConfigureAwait(false);

                    if (refinement.Success)
                    {
                        _logger?.LogInformation("Refinement: {Explanation}", refinement.Explanation);
                        // Clear atoms to force re-generation with refined feedback
                        currentMeTTaAtoms = null;
                    }
                    else
                    {
                        currentMeTTaAtoms = null;
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
                    ct).ConfigureAwait(false);
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

    private async Task<string> GenerateMeTTaAtomsAsync(
        string description,
        string? sampleInput,
        CancellationToken ct)
    {
        string prompt = BuildMeTTaGenerationPrompt(description, sampleInput);

        ChatResponse? response = null;
        await foreach (var chunk in _llm.GenerateAsync(prompt, cancellationToken: ct).ConfigureAwait(false))
        {
            response = chunk;
        }

        string generated = response?.ToString() ?? string.Empty;

        return ExtractMeTTaFromResponse(generated);
    }

    private static string BuildMeTTaGenerationPrompt(string description, string? sampleInput)
    {
        var prompt = $"""
            Generate a MeTTa grammar specification for the following language:

            {description}

            You must output MeTTa atoms using these constructors:

            - (MkTerminal "NAME") — for literal token definitions
            - (MkRegexTerminal "NAME" "regex-pattern") — for regex-based token definitions
            - (MkProduction "ruleName" (Cons (Cons "sym1" (Cons "sym2" Nil)) (Cons (Cons "sym3" Nil) Nil))) — parser rule with alternatives
              Each alternative is a Cons-list of symbol names. Multiple alternatives are a Cons-list of alternatives.
            - (MkGrammar "GrammarName" "startRule" (Cons prod1 (Cons prod2 Nil))) — wraps all productions

            Rules:
            - Parser rule names must be lowercase (e.g., "expr", "statement")
            - Terminal/lexer rule names must be UPPERCASE (e.g., "NUMBER", "PLUS")
            - The start rule is the first parser rule
            - Avoid left recursion: use "exprPrime" patterns instead
            - Include all necessary terminals for the language
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

        prompt += """

            Example for a simple arithmetic language:
            ```metta
            (MkRegexTerminal "NUMBER" "[0-9]+")
            (MkTerminal "PLUS")
            (MkTerminal "STAR")
            (MkProduction "expr" (Cons (Cons "term" (Cons "exprPrime" Nil)) Nil))
            (MkProduction "exprPrime" (Cons (Cons "PLUS" (Cons "term" (Cons "exprPrime" Nil))) (Cons Nil Nil)))
            (MkProduction "term" (Cons (Cons "NUMBER" Nil) Nil))
            (MkGrammar "Arithmetic" "expr" (Cons (MkProduction "expr" ...) ...))
            ```

            Respond with ONLY the MeTTa atoms, no explanations.
            """;

        return prompt;
    }

    private static string ExtractMeTTaFromResponse(string response)
    {
        // Try to extract from markdown code block
        var codeBlockMatch = CodeBlockRegex().Match(response);

        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // Try to find MeTTa atoms directly (lines starting with '(')
        var atomLines = response.Split('\n')
            .Where(l => l.TrimStart().StartsWith('('))
            .ToList();

        if (atomLines.Count > 0)
        {
            return string.Join("\n", atomLines).Trim();
        }

        // Return as-is
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

            // Use reflection to instantiate the ANTLR pipeline
            // This works with any generated grammar without knowing types at compile time
            var antlrInputStreamType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
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
                    catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
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
                .FirstOrDefault(m => m.DeclaringType == compiled.ParserType
                    && m.GetParameters().Length == 0
                    && m.ReturnType.Name.EndsWith("Context"));

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
        catch (InvalidOperationException ex)
        {
            return new ParseAttemptResult(
                false,
                ex.Message,
                new ParseFailureInfo("unknown", "unknown", 0, 0, input));
        }
    }

    private sealed record ParseAttemptResult(bool Success, string? Error, ParseFailureInfo? Failure);

    [GeneratedRegex(@"```(?:metta|lisp|scheme)?\s*\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();
}
