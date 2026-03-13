// <copyright file="GrammarValidationStep.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Pipeline.Grammar;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline step that validates ANTLR grammar strings through the upstream
/// Hyperon sidecar, analogous to <see cref="Ouroboros.Pipeline.Verification.MeTTaVerificationStep"/>
/// but for grammars rather than agent plans.
/// </summary>
/// <remarks>
/// This step integrates into the reasoning pipeline to provide symbolic
/// guard rails for grammar correctness before compilation is attempted.
/// </remarks>
public sealed class GrammarValidationStep
{
    private readonly IGrammarValidator _validator;
    private readonly HyperonMeTTaEngine? _engine;
    private readonly ILogger<GrammarValidationStep>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrammarValidationStep"/> class.
    /// </summary>
    /// <param name="validator">The grammar validation service.</param>
    /// <param name="engine">Optional Hyperon engine for recording validation events.</param>
    /// <param name="logger">Optional logger.</param>
    public GrammarValidationStep(
        IGrammarValidator validator,
        HyperonMeTTaEngine? engine = null,
        ILogger<GrammarValidationStep>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Validates a grammar and returns the validation result.
    /// </summary>
    /// <param name="grammarG4">The ANTLR4 grammar to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    public async Task<GrammarValidationResult> ValidateAsync(string grammarG4, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grammarG4);

        var result = await _validator.ValidateAsync(grammarG4, ct).ConfigureAwait(false);

        // Record in AtomSpace
        if (_engine != null)
        {
            string status = result.IsValid ? "Valid" : "Invalid";
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("GrammarValidation"),
                Atom.Sym(status),
                Atom.Sym(result.Issues.Count.ToString())));

            foreach (var issue in result.Issues)
            {
                _engine.AddAtom(Atom.Expr(
                    Atom.Sym("IssueIn"),
                    Atom.Sym(issue.RuleName),
                    Atom.Sym(issue.Kind.ToString())));
            }
        }

        if (!result.IsValid)
        {
            _logger?.LogInformation(
                "Grammar validation found {Count} issue(s): {Issues}",
                result.Issues.Count,
                string.Join("; ", result.Issues.Select(i => $"{i.Kind}:{i.RuleName}")));
        }

        return result;
    }

    /// <summary>
    /// Validates and auto-corrects a grammar, returning the corrected version.
    /// </summary>
    /// <param name="grammarG4">The ANTLR4 grammar to validate and correct.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validated (and possibly corrected) grammar string.</returns>
    public async Task<string> ValidateAndCorrectAsync(string grammarG4, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(grammarG4, ct).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return grammarG4;
        }

        var correction = await _validator.CorrectAsync(grammarG4, validation.Issues, ct).ConfigureAwait(false);
        if (correction.Success)
        {
            _logger?.LogInformation(
                "Applied {Count} correction(s): {Corrections}",
                correction.CorrectionsApplied.Count,
                string.Join("; ", correction.CorrectionsApplied));

            return correction.CorrectedGrammarG4;
        }

        _logger?.LogWarning("Grammar correction failed, returning original grammar");
        return grammarG4;
    }
}
