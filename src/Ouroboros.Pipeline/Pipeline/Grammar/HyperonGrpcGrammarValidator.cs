// <copyright file="HyperonGrpcGrammarValidator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Pipeline.Grammar;

using System.Net.Http;
using global::Grpc.Net.Client;
using Ouroboros.Pipeline.Grammar.Grpc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Grammar validation service backed by the upstream Hyperon sidecar via gRPC.
/// </summary>
/// <remarks>
/// Communicates with the Python Hyperon sidecar process which wraps the full
/// MeTTa engine for grammar validation, correction, and composition. Falls back
/// gracefully when the sidecar is unavailable.
/// </remarks>
public sealed class HyperonGrpcGrammarValidator : IGrammarValidator, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly HyperonGrammarService.HyperonGrammarServiceClient _client;
    private readonly ILogger<HyperonGrpcGrammarValidator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonGrpcGrammarValidator"/> class.
    /// </summary>
    /// <param name="endpoint">The gRPC endpoint (e.g. "http://localhost:50051").</param>
    /// <param name="logger">Optional logger.</param>
    public HyperonGrpcGrammarValidator(string endpoint, ILogger<HyperonGrpcGrammarValidator>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        _logger = logger;
        _channel = GrpcChannel.ForAddress(endpoint);
        _client = new HyperonGrammarService.HyperonGrammarServiceClient(_channel);
    }

    /// <summary>
    /// Initializes a new instance with an existing gRPC channel.
    /// </summary>
    /// <param name="channel">The gRPC channel.</param>
    /// <param name="logger">Optional logger.</param>
    public HyperonGrpcGrammarValidator(GrpcChannel channel, ILogger<HyperonGrpcGrammarValidator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _logger = logger;
        _client = new HyperonGrammarService.HyperonGrammarServiceClient(_channel);
    }

    /// <inheritdoc/>
    public async Task<GrammarValidationResult> ValidateAsync(string grammarG4, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grammarG4);

        var request = new ValidateGrammarRequest { GrammarG4 = grammarG4 };
        var response = await _client.ValidateGrammarAsync(request, cancellationToken: ct);

        var issues = response.Issues
            .Select(MapIssue)
            .ToList();

        return new GrammarValidationResult(response.IsValid, issues);
    }

    /// <inheritdoc/>
    public async Task<GrammarCorrectionResult> CorrectAsync(
        string grammarG4,
        IReadOnlyList<GrammarIssue> knownIssues,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grammarG4);

        var request = new CorrectGrammarRequest { GrammarG4 = grammarG4 };
        foreach (var issue in knownIssues)
        {
            request.KnownIssues.Add(new Grpc.GrammarIssue
            {
                Severity = MapSeverityToGrpc(issue.Severity),
                RuleName = issue.RuleName,
                Description = issue.Description,
                Kind = MapKindToGrpc(issue.Kind),
            });
        }

        var response = await _client.CorrectGrammarAsync(request, cancellationToken: ct);

        return new GrammarCorrectionResult(
            response.Success,
            response.CorrectedGrammarG4,
            response.CorrectionsApplied.ToList(),
            response.RemainingIssues.Select(MapIssue).ToList());
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ComposedGrammarG4, IReadOnlyList<string> ConflictsResolved)> ComposeAsync(
        IReadOnlyList<string> fragments,
        string grammarName,
        CancellationToken ct = default)
    {
        var request = new ComposeGrammarsRequest { GrammarName = grammarName };
        request.GrammarFragments.AddRange(fragments);

        var response = await _client.ComposeGrammarsAsync(request, cancellationToken: ct);

        return (response.Success, response.ComposedGrammarG4, response.ConflictsResolved.ToList());
    }

    /// <inheritdoc/>
    public async Task<GrammarRefinementResult> RefineAsync(
        string grammarG4,
        ParseFailureInfo failure,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grammarG4);

        var request = new RefineGrammarRequest
        {
            GrammarG4 = grammarG4,
            Failure = new Grpc.ParseFailureInfo
            {
                OffendingToken = failure.OffendingToken,
                ExpectedTokens = failure.ExpectedTokens,
                Line = failure.Line,
                Column = failure.Column,
                InputSnippet = failure.InputSnippet,
            },
        };

        var response = await _client.RefineGrammarAsync(request, cancellationToken: ct);

        return new GrammarRefinementResult(
            response.Success,
            response.RefinedGrammarG4,
            response.RefinementExplanation);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string GrammarId)> StoreProvenGrammarAsync(
        string description,
        string grammarG4,
        IReadOnlyList<string> sampleInputs,
        CancellationToken ct = default)
    {
        var request = new StoreProvenGrammarRequest
        {
            Description = description,
            GrammarG4 = grammarG4,
        };
        request.SampleInputs.AddRange(sampleInputs);

        var response = await _client.StoreProvenGrammarAsync(request, cancellationToken: ct);
        return (response.Success, response.GrammarId);
    }

    /// <inheritdoc/>
    public async Task<(bool Found, string GrammarG4, string GrammarId, double SimilarityScore)> RetrieveGrammarAsync(
        string description,
        CancellationToken ct = default)
    {
        var request = new RetrieveGrammarRequest { Description = description };
        var response = await _client.RetrieveGrammarAsync(request, cancellationToken: ct);
        return (response.Found, response.GrammarG4, response.GrammarId, response.SimilarityScore);
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.HealthCheckAsync(new HealthCheckRequest(), cancellationToken: ct);
            return response.Healthy;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Hyperon grammar sidecar health check failed");
            return false;
        }
    }

    // --- Logic Transfer Object (LTO) Operations ---

    /// <inheritdoc/>
    public async Task<(bool Success, string GrammarG4, IReadOnlyList<string> Notes)> AtomsToGrammarAsync(
        string mettaAtoms,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mettaAtoms);

        var request = new AtomsToGrammarRequest { MettaAtoms = mettaAtoms };
        var response = await _client.AtomsToGrammarAsync(request, cancellationToken: ct);

        return (response.Success, response.GrammarG4, response.Notes.ToList());
    }

    /// <inheritdoc/>
    public async Task<(GrammarValidationResult Result, IReadOnlyList<string> ValidationNotes)> ValidateAtomsAsync(
        string mettaAtoms,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mettaAtoms);

        var request = new ValidateAtomsRequest { MettaAtoms = mettaAtoms };
        var response = await _client.ValidateAtomsAsync(request, cancellationToken: ct);

        var issues = response.Issues.Select(MapIssue).ToList();
        var result = new GrammarValidationResult(response.IsValid, issues);

        return (result, response.ValidationNotes.ToList());
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string CorrectedMeTTaAtoms, IReadOnlyList<string> CorrectionsApplied, IReadOnlyList<GrammarIssue> RemainingIssues)> CorrectAtomsAsync(
        string mettaAtoms,
        IReadOnlyList<GrammarIssue> knownIssues,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mettaAtoms);

        var request = new CorrectAtomsRequest { MettaAtoms = mettaAtoms };
        foreach (var issue in knownIssues)
        {
            request.KnownIssues.Add(new Grpc.GrammarIssue
            {
                Severity = MapSeverityToGrpc(issue.Severity),
                RuleName = issue.RuleName,
                Description = issue.Description,
                Kind = MapKindToGrpc(issue.Kind),
            });
        }

        var response = await _client.CorrectAtomsAsync(request, cancellationToken: ct);

        return (
            response.Success,
            response.CorrectedMettaAtoms,
            response.CorrectionsApplied.ToList(),
            response.RemainingIssues.Select(MapIssue).ToList());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _channel.Dispose();
    }

    private static GrammarIssue MapIssue(Grpc.GrammarIssue grpcIssue)
    {
        return new GrammarIssue(
            MapSeverityFromGrpc(grpcIssue.Severity),
            grpcIssue.RuleName,
            grpcIssue.Description,
            MapKindFromGrpc(grpcIssue.Kind));
    }

    private static GrammarIssueSeverity MapSeverityFromGrpc(Grpc.GrammarIssueSeverity severity)
        => severity switch
        {
            Grpc.GrammarIssueSeverity.Error => GrammarIssueSeverity.Error,
            _ => GrammarIssueSeverity.Warning,
        };

    private static Grpc.GrammarIssueSeverity MapSeverityToGrpc(GrammarIssueSeverity severity)
        => severity switch
        {
            GrammarIssueSeverity.Error => Grpc.GrammarIssueSeverity.Error,
            _ => Grpc.GrammarIssueSeverity.Warning,
        };

    private static GrammarIssueKind MapKindFromGrpc(Grpc.GrammarIssueKind kind)
        => kind switch
        {
            Grpc.GrammarIssueKind.LeftRecursion => GrammarIssueKind.LeftRecursion,
            Grpc.GrammarIssueKind.UnreachableRule => GrammarIssueKind.UnreachableRule,
            Grpc.GrammarIssueKind.FirstSetConflict => GrammarIssueKind.FirstSetConflict,
            Grpc.GrammarIssueKind.MissingRule => GrammarIssueKind.MissingRule,
            Grpc.GrammarIssueKind.SyntaxError => GrammarIssueKind.SyntaxError,
            Grpc.GrammarIssueKind.Ambiguity => GrammarIssueKind.Ambiguity,
            _ => GrammarIssueKind.Unspecified,
        };

    private static Grpc.GrammarIssueKind MapKindToGrpc(GrammarIssueKind kind)
        => kind switch
        {
            GrammarIssueKind.LeftRecursion => Grpc.GrammarIssueKind.LeftRecursion,
            GrammarIssueKind.UnreachableRule => Grpc.GrammarIssueKind.UnreachableRule,
            GrammarIssueKind.FirstSetConflict => Grpc.GrammarIssueKind.FirstSetConflict,
            GrammarIssueKind.MissingRule => Grpc.GrammarIssueKind.MissingRule,
            GrammarIssueKind.SyntaxError => Grpc.GrammarIssueKind.SyntaxError,
            GrammarIssueKind.Ambiguity => Grpc.GrammarIssueKind.Ambiguity,
            _ => Grpc.GrammarIssueKind.Unspecified,
        };
}
