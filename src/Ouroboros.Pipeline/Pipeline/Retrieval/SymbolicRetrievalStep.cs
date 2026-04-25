// <copyright file="SymbolicRetrievalStep.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Retrieval;

using Ouroboros.Domain.Vectors;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Pipeline step that performs hybrid retrieval using both symbolic (MeTTa) and semantic (vector) search.
/// Symbolic queries provide exact matches for state-based queries, while vectors handle semantic similarity.
/// </summary>
public sealed class SymbolicRetrievalStep
{
    private readonly IMeTTaEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicRetrievalStep"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic retrieval.</param>
    public SymbolicRetrievalStep(IMeTTaEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        this._engine = engine;
    }

    /// <summary>
    /// Performs symbolic retrieval based on document status.
    /// </summary>
    /// <param name="status">The status to filter by (e.g., "Outdated", "Current").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByStatusAsync(
        string status,
        CancellationToken ct = default)
    {
        string query = $"!(match &self (Status $doc (State \"{status}\")) $doc)";
        return await this.ExecuteSymbolicQueryAsync(query, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs symbolic retrieval based on document topic.
    /// </summary>
    /// <param name="topic">The topic to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByTopicAsync(
        string topic,
        CancellationToken ct = default)
    {
        string query = $"!(match &self (Topic $doc (Concept \"{topic}\")) $doc)";
        return await this.ExecuteSymbolicQueryAsync(query, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs compound symbolic retrieval filtering by both status and topic.
    /// This is where symbolic reasoning excels over pure vector search.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="topic">The topic to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByStatusAndTopicAsync(
        string status,
        string topic,
        CancellationToken ct = default)
    {
        // MeTTa compound query - exact match on both conditions
        string query = $@"!(match &self 
            (, 
              (Status $doc (State ""{status}""))
              (Topic $doc (Concept ""{topic}""))
            ) 
            $doc)";
        
        return await this.ExecuteSymbolicQueryAsync(query, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs hybrid retrieval combining symbolic and semantic search.
    /// </summary>
    /// <param name="branch">The pipeline branch containing the vector store.</param>
    /// <param name="embedModel">The embedding model.</param>
    /// <param name="query">The natural language query.</param>
    /// <param name="status">Optional status filter for symbolic search.</param>
    /// <param name="topic">Optional topic filter for symbolic search.</param>
    /// <param name="semanticK">Number of semantic results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A hybrid retrieval result.</returns>
    public async Task<Result<HybridRetrievalResult, string>> HybridRetrieveAsync(
        PipelineBranch branch,
        IEmbeddingModel embedModel,
        string query,
        string? status = null,
        string? topic = null,
        int semanticK = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(embedModel);
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            // Step 1: Symbolic retrieval (exact matches)
            List<string> symbolicMatches = new();

            if (!string.IsNullOrEmpty(status) && !string.IsNullOrEmpty(topic))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByStatusAndTopicAsync(status, topic, ct).ConfigureAwait(false);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }
            else if (!string.IsNullOrEmpty(status))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByStatusAsync(status, ct).ConfigureAwait(false);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }
            else if (!string.IsNullOrEmpty(topic))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByTopicAsync(topic, ct).ConfigureAwait(false);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }

            // Step 2: Semantic retrieval (vector similarity)
            IReadOnlyCollection<Document> semanticMatches = 
                await branch.Store.GetSimilarDocuments(embedModel, query, amount: semanticK, cancellationToken: ct).ConfigureAwait(false);

            HybridRetrievalResult hybridResult = new(
                query,
                symbolicMatches,
                semanticMatches.ToList());

            return Result<HybridRetrievalResult, string>.Success(hybridResult);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<HybridRetrievalResult, string>.Failure(
                $"Hybrid retrieval failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a Kleisli arrow for hybrid retrieval.
    /// </summary>
    /// <param name="embedModel">The embedding model.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="topic">Optional topic filter.</param>
    /// <param name="semanticK">Number of semantic results.</param>
    /// <returns>A step that performs hybrid retrieval.</returns>
    public Step<(PipelineBranch Branch, string Query), Result<HybridRetrievalResult, string>> AsArrow(
        IEmbeddingModel embedModel,
        string? status = null,
        string? topic = null,
        int semanticK = 5)
        => input => this.HybridRetrieveAsync(input.Branch, embedModel, input.Query, status, topic, semanticK);

    /// <summary>
    /// Executes a symbolic query and parses the results.
    /// </summary>
    private async Task<Result<IReadOnlyList<string>, string>> ExecuteSymbolicQueryAsync(
        string query,
        CancellationToken ct)
    {
        Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct).ConfigureAwait(false);

        return result.Match(
            success => Result<IReadOnlyList<string>, string>.Success(ParseDocumentIds(success)),
            error => Result<IReadOnlyList<string>, string>.Failure(error));
    }

    private static readonly System.Text.RegularExpressions.Regex DocPattern =
        new(@"\(Doc\s+""([^""]+)""\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex QuotedStringPattern =
        new(@"""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Parses document IDs from MeTTa query results.
    /// </summary>
    private static IReadOnlyList<string> ParseDocumentIds(string mettaResult)
    {
        // MeTTa returns results in various formats like:
        // [(Doc "deployment.md")] or (Doc "deployment.md") or just document identifiers
        List<string> ids = DocPattern.Matches(mettaResult)
            .Cast<System.Text.RegularExpressions.Match>()
            .Where(match => match.Groups.Count > 1)
            .Select(match => match.Groups[1].Value)
            .ToList();

        // Also try simple string extraction for direct matches
        if (ids.Count == 0)
        {
            // Try to extract quoted strings
            ids = QuotedStringPattern.Matches(mettaResult)
                .Cast<System.Text.RegularExpressions.Match>()
                .Where(match => match.Groups.Count > 1)
                .Select(match => match.Groups[1].Value)
                .ToList();
        }

        return ids;
    }
}
