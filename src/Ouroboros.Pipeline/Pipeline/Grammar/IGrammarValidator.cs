// <copyright file="IGrammarValidator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Interface for grammar validation, correction, and composition services.
/// Backed by the upstream Hyperon sidecar via gRPC.
/// </summary>
public interface IGrammarValidator
{
    /// <summary>
    /// Validates an ANTLR4 grammar string for structural issues.
    /// </summary>
    /// <param name="grammarG4">The ANTLR4 grammar content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result with any issues found.</returns>
    Task<GrammarValidationResult> ValidateAsync(string grammarG4, CancellationToken ct = default);

    /// <summary>
    /// Corrects a grammar by applying MeTTa rewriting rules.
    /// </summary>
    /// <param name="grammarG4">The grammar to correct.</param>
    /// <param name="knownIssues">Issues discovered during validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Correction result with the fixed grammar.</returns>
    Task<GrammarCorrectionResult> CorrectAsync(
        string grammarG4,
        IReadOnlyList<GrammarIssue> knownIssues,
        CancellationToken ct = default);

    /// <summary>
    /// Composes multiple grammar fragments into a single coherent grammar.
    /// </summary>
    /// <param name="fragments">Grammar fragments to compose.</param>
    /// <param name="grammarName">Name for the composed grammar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed grammar and any conflicts resolved.</returns>
    Task<(bool Success, string ComposedGrammarG4, IReadOnlyList<string> ConflictsResolved)> ComposeAsync(
        IReadOnlyList<string> fragments,
        string grammarName,
        CancellationToken ct = default);

    /// <summary>
    /// Refines a grammar based on parse failure feedback.
    /// </summary>
    /// <param name="grammarG4">The current grammar.</param>
    /// <param name="failure">Information about the parse failure.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Refinement result with the improved grammar.</returns>
    Task<GrammarRefinementResult> RefineAsync(
        string grammarG4,
        ParseFailureInfo failure,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a proven grammar for future retrieval.
    /// </summary>
    /// <param name="description">What this grammar parses.</param>
    /// <param name="grammarG4">The proven grammar content.</param>
    /// <param name="sampleInputs">Inputs that were successfully parsed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success and assigned grammar ID.</returns>
    Task<(bool Success, string GrammarId)> StoreProvenGrammarAsync(
        string description,
        string grammarG4,
        IReadOnlyList<string> sampleInputs,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the closest matching proven grammar for a description.
    /// </summary>
    /// <param name="description">Description of the desired grammar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Match result with similarity score.</returns>
    Task<(bool Found, string GrammarG4, string GrammarId, double SimilarityScore)> RetrieveGrammarAsync(
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Checks the health of the grammar validation service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the service is healthy.</returns>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);

    // --- Logic Transfer Object (LTO) Operations ---
    // These methods accept MeTTa atoms as formal specifications and convert/validate/correct
    // them via the Hyperon sidecar. MeTTa atoms are "Logic Transfer Objects" — they carry
    // formally verifiable logic over the wire, not just data.

    /// <summary>
    /// Converts MeTTa grammar spec atoms to an ANTLR4 .g4 grammar string.
    /// The atoms use MkGrammar, MkProduction, MkTerminal, and MkRegexTerminal constructors.
    /// </summary>
    /// <param name="mettaAtoms">MeTTa source containing grammar spec atoms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success flag, the generated .g4 grammar, and conversion notes.</returns>
    Task<(bool Success, string GrammarG4, IReadOnlyList<string> Notes)> AtomsToGrammarAsync(
        string mettaAtoms,
        CancellationToken ct = default);

    /// <summary>
    /// Validates MeTTa grammar spec atoms against structural rules in the AtomSpace.
    /// </summary>
    /// <param name="mettaAtoms">MeTTa source containing grammar spec atoms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result with issues and notes.</returns>
    Task<(GrammarValidationResult Result, IReadOnlyList<string> ValidationNotes)> ValidateAtomsAsync(
        string mettaAtoms,
        CancellationToken ct = default);

    /// <summary>
    /// Corrects MeTTa grammar spec atoms by applying symbolic rewriting rules.
    /// </summary>
    /// <param name="mettaAtoms">MeTTa source containing grammar spec atoms.</param>
    /// <param name="knownIssues">Issues discovered during atom validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success flag, corrected MeTTa atoms, corrections applied, and remaining issues.</returns>
    Task<(bool Success, string CorrectedMeTTaAtoms, IReadOnlyList<string> CorrectionsApplied, IReadOnlyList<GrammarIssue> RemainingIssues)> CorrectAtomsAsync(
        string mettaAtoms,
        IReadOnlyList<GrammarIssue> knownIssues,
        CancellationToken ct = default);
}
