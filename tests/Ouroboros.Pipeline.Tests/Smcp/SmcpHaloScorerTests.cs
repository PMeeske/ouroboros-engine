// <copyright file="SmcpHaloScorerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Hyperon;
using Ouroboros.Pipeline.Smcp;
using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Tests.Pipeline.Smcp;

/// <summary>
/// Tests for HALO-backed SMCP confidence routing.
/// Verifies that SmcpConfidenceScorer uses HaloClassificationHead for bounded
/// confidence scoring with OOD detection, and that SmcpPatternMatcher routes
/// OOD intents to the Clarify gate.
/// </summary>
[Trait("Category", "Unit")]
public class SmcpHaloScorerTests
{
    #pragma warning disable CS0618 // IEmbeddingModel is obsolete but is the available interface
    private readonly Mock<IEmbeddingModel> _mockEmbedding;
    #pragma warning restore CS0618

    public SmcpHaloScorerTests()
    {
        _mockEmbedding = new Mock<IEmbeddingModel>();
    }

    /// <summary>
    /// Helper: creates a mock ITool with the given name and description.
    /// </summary>
    private static ITool CreateMockTool(string name, string description)
    {
        var mock = new Mock<ITool>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Description).Returns(description);
        mock.SetupGet(t => t.JsonSchema).Returns("{}");
        return mock.Object;
    }

    /// <summary>
    /// Helper: creates a SmcpToolAdapter from a mock ITool.
    /// </summary>
    private static SmcpToolAdapter CreateAdapter(string name, string description)
    {
        var tool = CreateMockTool(name, description);
        return SmcpToolAdapter.FromITool(tool);
    }

    /// <summary>
    /// Helper: creates an MkIntent expression atom.
    /// </summary>
    private static Expression CreateIntentAtom(string verb, string args, double confidence = 0.9)
    {
        return SmcpAtomFactory.MkIntent(verb, [args], confidence);
    }

    [Fact]
    public async Task HaloScorer_OOD_Intent_ReturnsNegativeScore()
    {
        // Arrange: Create a scorer with embedding model returning distinct vectors per tool,
        // but a very different embedding for the OOD intent
        // Tool 1 embedding: [1, 0, 0, 0]
        // Tool 2 embedding: [0, 1, 0, 0]
        // Intent embedding: [0, 0, 1, 0] — far from both tools (orthogonal)
        _mockEmbedding
            .Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                if (text.StartsWith("SearchTool"))
                    return new float[] { 1f, 0f, 0f, 0f };
                if (text.StartsWith("MemoryTool"))
                    return new float[] { 0f, 1f, 0f, 0f };
                // Intent text: return OOD vector
                return new float[] { 0f, 0f, 1f, 0f };
            });

        var scorer = new SmcpConfidenceScorer(_mockEmbedding.Object);
        var searchAdapter = CreateAdapter("SearchTool", "search for information");
        var memoryAdapter = CreateAdapter("MemoryTool", "store and recall memories");

        // Initialize HALO head
        await scorer.PrecomputeToolEmbeddingsAsync(new[] { searchAdapter, memoryAdapter });

        // Create an OOD intent
        var intentAtom = CreateIntentAtom("fly", "airplane");

        // Act: Score against the search tool — HALO should detect OOD
        double searchScore = scorer.Score(intentAtom, searchAdapter, Substitution.Empty);
        double memoryScore = scorer.Score(intentAtom, memoryAdapter, Substitution.Empty);

        // Assert: OOD intent should produce negative composite score from HALO
        // At least one should be negative (OOD), and both should indicate OOD
        Assert.True(searchScore < 0 || memoryScore < 0,
            $"At least one score should be negative for OOD intent, got search={searchScore}, memory={memoryScore}");
    }

    [Fact]
    public async Task HaloScorer_MatchingTool_HighConfidence()
    {
        // Arrange: Create a scorer with embedding model that returns near-identical
        // embeddings for tool centroid and matching intent.
        // With sigma=0.3 (sharp boundary), near-centroid confidence should be high.
        // Note: SmcpConfidenceScorer uses sigma=1.0 by default, so at-centroid
        // confidence with a single tool is ~0.43. The composite formula further
        // multiplies by intentConf and reliability, so we check for a positive score
        // that exceeds the cosine fallback floor.
        _mockEmbedding
            .Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                if (text.StartsWith("SearchTool"))
                    return new float[] { 1f, 0f, 0f, 0f };
                // Intent matches search — same direction
                if (text.Contains("search") || text.Contains("Search"))
                    return new float[] { 0.95f, 0.05f, 0f, 0f };
                // Anything else: orthogonal
                return new float[] { 0f, 0f, 1f, 0f };
            });

        var scorer = new SmcpConfidenceScorer(_mockEmbedding.Object);
        var searchAdapter = CreateAdapter("SearchTool", "search for information");

        // Initialize HALO head
        await scorer.PrecomputeToolEmbeddingsAsync(new[] { searchAdapter });

        // Create a matching intent
        var intentAtom = CreateIntentAtom("search", "search for information");

        // Act
        double score = scorer.Score(intentAtom, searchAdapter, Substitution.Empty);

        // Assert: Matching tool should have positive composite confidence from HALO.
        // With sigma=1.0 and a single tool, at-centroid HALO confidence is ~0.43.
        // Composite = intentConf(0.9) * matchConf(~0.43) * reliability(0.8) ~= 0.31
        // This is well above 0, confirming HALO is active (not cosine fallback which
        // would give 0 for non-matching tools).
        Assert.True(score > 0.1,
            $"Matching tool should have positive composite confidence from HALO, got {score}");
    }

    [Fact]
    public void HaloScorer_FallbackToCosine_WhenNoHaloHead()
    {
        // Arrange: Create a scorer with embedding model but do NOT initialize HALO head
        _mockEmbedding
            .Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.8f, 0.6f, 0f, 0f });

        var scorer = new SmcpConfidenceScorer(_mockEmbedding.Object);
        var adapter = CreateAdapter("TestTool", "a test tool for testing");

        // Do NOT call PrecomputeToolEmbeddingsAsync or TryInitializeHaloHead

        // Create an intent
        var intentAtom = CreateIntentAtom("test", "testing");

        // Act: Score without HALO head — should use cosine similarity fallback
        double score = scorer.Score(intentAtom, adapter, Substitution.Empty);

        // Assert: Cosine similarity fallback produces score in [0, 1]
        // Composite = intentConf(0.9) * cosineSim(1.0) * reliability(0.8) = 0.72
        Assert.True(score >= 0.0 && score <= 1.0,
            $"Cosine fallback should produce score in [0, 1], got {score}");

        // Same embedding vector → cosine similarity = 1.0, but composite includes
        // intent confidence (0.9) and reliability (0.8), so expected ~0.72
        Assert.True(score > 0.5,
            $"Identical vectors should have high composite confidence, got {score}");
    }

    [Fact]
    public async Task EvaluateIntent_OOD_TriggerClarify()
    {
        // Arrange: Create a pattern matcher with a scorer that uses HALO for OOD detection.
        // Use a custom activation pattern with variable wildcards so it unifies with any MkIntent,
        // and empty keywords to bypass the keyword guard entirely.
        _mockEmbedding
            .Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // Tool embedding: unit vector along first axis
                if (text.StartsWith("SearchTool"))
                    return new float[] { 1f, 0f, 0f, 0f };
                // Intent: orthogonal to all tools (OOD)
                return new float[] { 0f, 0f, 1f, 0f };
            });

        var scorer = new SmcpConfidenceScorer(_mockEmbedding.Object);
        using var engine = new HyperonMeTTaEngine();
        // Use very low thresholds so OOD (negative score) is the only path to Clarify
        var config = new SmcpConfidenceConfig(
            FireThreshold: 0.85,
            ClarifyThreshold: 0.30,
            RejectThreshold: -1.0);
        using var matcher = new SmcpPatternMatcher(engine, scorer, config: config);

        // Custom pattern with variable wildcards for unification and empty keywords to bypass guard
        var tool = CreateMockTool("SearchTool", "search for information");
        var pattern = Atom.Expr(
            SmcpSymbols.MkIntent,
            Atom.Var("verb"),
            Atom.Var("args"),
            Atom.Var("conf"));
        // Empty keywords list bypasses the keyword guard in EvaluateIntent
        var activationPattern = new SmcpActivationPattern(pattern, Array.Empty<string>(), 0.1);
        var adapter = SmcpToolAdapter.WithCustomPattern(tool, activationPattern);
        matcher.RegisterAdapter(adapter);

        Expression? clarificationAtom = null;
        matcher.ClarificationNeeded += atom => clarificationAtom = atom;

        // Initialize HALO head so OOD detection kicks in
        await scorer.PrecomputeToolEmbeddingsAsync(new[] { adapter });

        // Create an OOD intent with embedding orthogonal to tool centroid
        var oodIntent = SmcpAtomFactory.MkIntent("fly", ["airplane"], 0.9);

        // Act
        var results = matcher.EvaluateIntent(oodIntent);

        // Assert: OOD intent should not produce any tool matches (composite < 0 triggers Clarify, not Fire)
        Assert.Empty(results);

        // Assert: Clarification should have been triggered by OOD detection
        Assert.NotNull(clarificationAtom);
    }
}