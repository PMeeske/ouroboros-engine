// <copyright file="VectorGraphFeedbackLoopTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Moq;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Core.Configuration;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class VectorGraphFeedbackLoopTests : IAsyncDisposable
{
    private readonly QdrantDagStore _store;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly QdrantClient _client;

    public VectorGraphFeedbackLoopTests()
    {
        _client = new QdrantClient("localhost", 6334);
        var registry = new Mock<IQdrantCollectionRegistry>();
        registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.DagNodes))
            .Returns("test_dag_nodes");
        registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.DagEdges))
            .Returns("test_dag_edges");
        var settings = new QdrantSettings
        {
            GrpcEndpoint = "http://localhost:6334",
            DefaultVectorSize = 384,
            UseHttps = false
        };
        _store = new QdrantDagStore(_client, registry.Object, settings);
        _mettaEngine = Substitute.For<IMeTTaEngine>();
        _embeddingModel = Substitute.For<IEmbeddingModel>();
    }

    #region Constructor Tests

    [Fact]
    public void Ctor_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VectorGraphFeedbackLoop(null!, _mettaEngine, _embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public void Ctor_NullMeTTaEngine_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VectorGraphFeedbackLoop(_store, null!, _embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mettaEngine");
    }

    [Fact]
    public void Ctor_NullEmbeddingModel_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VectorGraphFeedbackLoop(_store, _mettaEngine, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("embeddingModel");
    }

    [Fact]
    public void Ctor_NullConfig_UsesDefaults()
    {
        // Act
        var sut = new VectorGraphFeedbackLoop(_store, _mettaEngine, _embeddingModel, null);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithCustomConfig_DoesNotThrow()
    {
        // Arrange
        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.8f,
            RotationThreshold: 0.5f,
            MaxModificationsPerCycle: 20,
            AutoPersist: false);

        // Act
        var sut = new VectorGraphFeedbackLoop(_store, _mettaEngine, _embeddingModel, config);

        // Assert
        sut.Should().NotBeNull();
    }

    #endregion

    #region ExecuteCycleAsync Tests

    [Fact]
    public async Task ExecuteCycleAsync_NullDag_ReturnsFailure()
    {
        // Arrange
        var sut = new VectorGraphFeedbackLoop(_store, _mettaEngine, _embeddingModel);

        // Act
        var result = await sut.ExecuteCycleAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    #endregion

    #region ParseModifications Tests (via reflection)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseModifications_EmptyOrNull_ReturnsEmptyList(string? input)
    {
        // Act
        var result = InvokeParseModifications(input!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseModifications_EmptyBrackets_ReturnsEmptyList()
    {
        // Act
        var result = InvokeParseModifications("[[]]");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseModifications_EmptyParens_ReturnsEmptyList()
    {
        // Act
        var result = InvokeParseModifications("()");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseModifications_StrengthenWithTwoGuids_ReturnsTwoModifications()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var input = $"(strengthen \"{guid1}\" \"{guid2}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.ModificationType == "strengthen");
    }

    [Fact]
    public void ParseModifications_StrengthenEdge_ReturnsTwoModifications()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var input = $"(strengthen-edge \"{guid1}\" \"{guid2}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.ModificationType == "strengthen");
    }

    [Fact]
    public void ParseModifications_WeakenOutgoingEdges_ReturnsSingleModification()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var input = $"(weaken-outgoing-edges \"{guid1}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().ContainSingle();
        result[0].ModificationType.Should().Be("weaken");
        result[0].NodeId.Should().Be(guid1);
    }

    [Fact]
    public void ParseModifications_Weaken_ReturnsSingleModification()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var input = $"(weaken \"{guid1}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().ContainSingle();
        result[0].ModificationType.Should().Be("weaken");
    }

    [Fact]
    public void ParseModifications_MergeSinks_ReturnsTwoModifications()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var input = $"(merge-sinks \"{guid1}\" \"{guid2}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.ModificationType == "merge");
    }

    [Fact]
    public void ParseModifications_Merge_ReturnsTwoModifications()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var input = $"(merge \"{guid1}\" \"{guid2}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.ModificationType == "merge");
    }

    [Fact]
    public void ParseModifications_UnknownOperation_StillRecordsNodes()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var input = $"(custom-op \"{guid1}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().ContainSingle();
        result[0].ModificationType.Should().Be("custom-op");
        result[0].NodeId.Should().Be(guid1);
    }

    [Fact]
    public void ParseModifications_NoGuidArgs_ReturnsEmpty()
    {
        // Arrange
        var input = "(strengthen not-a-guid also-not-a-guid)";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseModifications_NestedBrackets_StripsCorrectly()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var input = $"[[(strengthen \"{guid1}\" \"{Guid.NewGuid()}\")]]";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ParseModifications_MultipleExpressions_ParsesAll()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();
        var input = $"(weaken \"{guid1}\") (strengthen \"{guid2}\" \"{guid3}\")";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ParseModifications_GuidsWithoutQuotes_AreAlsoParsed()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var input = $"(weaken {guid1})";

        // Act
        var result = InvokeParseModifications(input);

        // Assert
        result.Should().ContainSingle();
        result[0].NodeId.Should().Be(guid1);
    }

    #endregion

    #region TokenizeSExpression Tests (via reflection)

    [Fact]
    public void TokenizeSExpression_SimpleTokens_SplitsOnWhitespace()
    {
        // Act
        var result = InvokeTokenize("strengthen arg1 arg2");

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("strengthen");
        result[1].Should().Be("arg1");
        result[2].Should().Be("arg2");
    }

    [Fact]
    public void TokenizeSExpression_QuotedString_PreservedAsSingleToken()
    {
        // Act
        var result = InvokeTokenize("op \"quoted value\" arg");

        // Assert
        result.Should().HaveCount(3);
        result[1].Should().Be("\"quoted value\"");
    }

    [Fact]
    public void TokenizeSExpression_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = InvokeTokenize("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void TokenizeSExpression_OnlyWhitespace_ReturnsEmpty()
    {
        // Act
        var result = InvokeTokenize("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void TokenizeSExpression_MultipleSpaces_HandledCorrectly()
    {
        // Act
        var result = InvokeTokenize("a   b   c");

        // Assert
        result.Should().HaveCount(3);
    }

    #endregion

    #region EscapeMeTTaString Tests (via reflection)

    [Fact]
    public void EscapeMeTTaString_NullInput_ReturnsNull()
    {
        // Act
        var result = InvokeEscape(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EscapeMeTTaString_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = InvokeEscape("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void EscapeMeTTaString_NoSpecialChars_ReturnsUnchanged()
    {
        // Act
        var result = InvokeEscape("hello-world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void EscapeMeTTaString_Backslash_IsEscaped()
    {
        // Act
        var result = InvokeEscape("path\\to\\file");

        // Assert
        result.Should().Be("path\\\\to\\\\file");
    }

    [Fact]
    public void EscapeMeTTaString_DoubleQuote_IsEscaped()
    {
        // Act
        var result = InvokeEscape("say \"hello\"");

        // Assert
        result.Should().Be("say \\\"hello\\\"");
    }

    [Fact]
    public void EscapeMeTTaString_BackslashAndQuote_BothEscaped()
    {
        // Act
        var result = InvokeEscape("a\\\"b");

        // Assert
        result.Should().Be("a\\\\\\\"b");
    }

    #endregion

    #region FeedbackLoopConfig Record Tests

    [Fact]
    public void FeedbackLoopConfig_Defaults_AreCorrect()
    {
        // Act
        var config = new FeedbackLoopConfig();

        // Assert
        config.DivergenceThreshold.Should().Be(0.5f);
        config.RotationThreshold.Should().Be(0.3f);
        config.MaxModificationsPerCycle.Should().Be(10);
        config.AutoPersist.Should().BeTrue();
    }

    [Fact]
    public void FeedbackLoopConfig_CustomValues_ArePreserved()
    {
        // Act
        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.9f,
            RotationThreshold: 0.7f,
            MaxModificationsPerCycle: 50,
            AutoPersist: false);

        // Assert
        config.DivergenceThreshold.Should().Be(0.9f);
        config.RotationThreshold.Should().Be(0.7f);
        config.MaxModificationsPerCycle.Should().Be(50);
        config.AutoPersist.Should().BeFalse();
    }

    [Fact]
    public void FeedbackLoopConfig_RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new FeedbackLoopConfig(0.5f, 0.3f, 10, true);
        var b = new FeedbackLoopConfig(0.5f, 0.3f, 10, true);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void FeedbackLoopConfig_RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new FeedbackLoopConfig(0.5f);
        var b = new FeedbackLoopConfig(0.9f);

        // Assert
        a.Should().NotBe(b);
    }

    #endregion

    #region FeedbackResult Record Tests

    [Fact]
    public void FeedbackResult_SetsAllProperties()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        var result = new FeedbackResult(
            NodesAnalyzed: 100,
            NodesModified: 5,
            SourceNodes: 10,
            SinkNodes: 8,
            CyclicNodes: 3,
            Duration: duration);

        // Assert
        result.NodesAnalyzed.Should().Be(100);
        result.NodesModified.Should().Be(5);
        result.SourceNodes.Should().Be(10);
        result.SinkNodes.Should().Be(8);
        result.CyclicNodes.Should().Be(3);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void FeedbackResult_RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(1);
        var a = new FeedbackResult(10, 2, 3, 4, 1, duration);
        var b = new FeedbackResult(10, 2, 3, 4, 1, duration);

        // Assert
        a.Should().Be(b);
    }

    #endregion

    #region Helper Methods

    private static List<dynamic> InvokeParseModifications(string input)
    {
        var method = typeof(VectorGraphFeedbackLoop)
            .GetMethod("ParseModifications", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("ParseModifications method must exist");

        var result = method!.Invoke(null, new object[] { input });
        // The result is a List<GraphModification> where GraphModification is a private nested record.
        // We need to reflect into its properties.
        var list = (System.Collections.IList)result!;
        var items = new List<dynamic>();
        foreach (var item in list)
        {
            var nodeIdProp = item.GetType().GetProperty("NodeId");
            var modTypeProp = item.GetType().GetProperty("ModificationType");
            items.Add(new GraphModificationProxy(
                (Guid)nodeIdProp!.GetValue(item)!,
                (string)modTypeProp!.GetValue(item)!));
        }

        return items;
    }

    private static List<string> InvokeTokenize(string input)
    {
        var method = typeof(VectorGraphFeedbackLoop)
            .GetMethod("TokenizeSExpression", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("TokenizeSExpression method must exist");

        return (List<string>)method!.Invoke(null, new object[] { input })!;
    }

    private static string InvokeEscape(string input)
    {
        var method = typeof(VectorGraphFeedbackLoop)
            .GetMethod("EscapeMeTTaString", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("EscapeMeTTaString method must exist");

        return (string)method!.Invoke(null, new object[] { input })!;
    }

    private sealed record GraphModificationProxy(Guid NodeId, string ModificationType);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        _client.Dispose();
    }

    #endregion
}
