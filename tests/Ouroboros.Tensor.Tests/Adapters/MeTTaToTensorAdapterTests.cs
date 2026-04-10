// <copyright file="MeTTaToTensorAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Hyperon;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Adapters;

namespace Ouroboros.Tests.Adapters;

/// <summary>
/// Test helper implementing IEmbeddingProvider with pre-computed embeddings.
/// </summary>
file sealed class TestEmbeddingProvider : IEmbeddingProvider
{
    private readonly Dictionary<string, float[]> _embeddings;

    public TestEmbeddingProvider(Dictionary<string, float[]> embeddings)
    {
        _embeddings = embeddings;
    }

    public float[]? TryGetEmbedding(string symbolName, int embeddingDim)
    {
        if (_embeddings.TryGetValue(symbolName, out var embedding) && embedding.Length == embeddingDim)
            return embedding;

        return null;
    }
}

[Trait("Category", "Unit")]
public sealed class MeTTaToTensorAdapterTests
{
    private readonly MeTTaToTensorAdapter _sut = new(CpuTensorBackend.Instance, embeddingDim: 128);

    /// <summary>
    /// Creates a test embedding vector with a sine-based pattern for deterministic testing.
    /// </summary>
    private static float[] MakeTestVector(int dim, float seed)
    {
        var result = new float[dim];
        for (int i = 0; i < dim; i++)
            result[i] = MathF.Sin(seed + i * 0.1f);
        return result;
    }

    [Fact]
    public void AtomToTensor_Symbol_ReturnsCachedVector()
    {
        // Arrange
        var symbol = Atom.Sym("hello");

        // Act
        using var tensor1 = _sut.AtomToTensor(symbol);
        using var tensor2 = _sut.AtomToTensor(symbol);

        // Assert
        tensor1.Shape.Should().Be(TensorShape.Of(128));
        tensor1.AsSpan().ToArray().Should().Equal(tensor2.AsSpan().ToArray());
    }

    [Fact]
    public void AtomToTensor_DifferentSymbols_ProduceDifferentVectors()
    {
        // Arrange
        var sym1 = Atom.Sym("cat");
        var sym2 = Atom.Sym("dog");

        // Act
        using var tensor1 = _sut.AtomToTensor(sym1);
        using var tensor2 = _sut.AtomToTensor(sym2);

        // Assert
        tensor1.AsSpan().ToArray().Should().NotEqual(tensor2.AsSpan().ToArray());
    }

    [Fact]
    public void AtomToTensor_SameSymbolName_ProducesSameVector()
    {
        // Arrange
        var sym1 = Atom.Sym("test");
        var sym2 = Atom.Sym("test");

        // Act
        using var tensor1 = _sut.AtomToTensor(sym1);
        using var tensor2 = _sut.AtomToTensor(sym2);

        // Assert
        tensor1.AsSpan().ToArray().Should().Equal(tensor2.AsSpan().ToArray());
    }

    [Fact]
    public void AtomToTensor_Variable_DifferentFromSymbol()
    {
        // Arrange
        var variable = Atom.Var("x");
        var symbol = Atom.Sym("x");

        // Act
        using var varTensor = _sut.AtomToTensor(variable);
        using var symTensor = _sut.AtomToTensor(symbol);

        // Assert
        // Variables have $ prefix internally, so they should differ
        varTensor.AsSpan().ToArray().Should().NotEqual(symTensor.AsSpan().ToArray());
    }

    [Fact]
    public void AtomToTensor_Expression_MeanPoolsChildren()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"));

        // Act
        using var tensor = _sut.AtomToTensor(expr);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(128));
        // Should be normalized
        var span = tensor.AsSpan().ToArray();
        var norm = MathF.Sqrt(span.Sum(v => v * v));
        norm.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void CosineSimilarity_SameSymbol_ReturnsOne()
    {
        // Arrange
        var sym = Atom.Sym("identical");

        // Act
        var similarity = _sut.CosineSimilarity(sym, sym);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void CosineSimilarity_DifferentSymbols_ReturnsValueBetweenMinusOneAndOne()
    {
        // Arrange
        var sym1 = Atom.Sym("alpha");
        var sym2 = Atom.Sym("beta");

        // Act
        var similarity = _sut.CosineSimilarity(sym1, sym2);

        // Assert
        similarity.Should().BeInRange(-1.0f, 1.0f);
        // Different symbols should not be identical
        similarity.Should().BeLessThan(1.0f);
    }

    [Fact]
    public void CosineSimilarity_Expressions_ComputesCorrectly()
    {
        // Arrange
        // Same symbols in different order - similarity should be reasonable
        var expr1 = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"), Atom.Sym("c"));
        var expr2 = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"), Atom.Sym("c"));

        // Act
        var similarity = _sut.CosineSimilarity(expr1, expr2);

        // Assert
        // Identical expressions should have high similarity
        similarity.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void BatchAtomsToTensor_MultipleAtoms_Returns2DTensor()
    {
        // Arrange
        var atoms = new Atom[]
        {
            Atom.Sym("first"),
            Atom.Sym("second"),
            Atom.Sym("third")
        };

        // Act
        using var tensor = _sut.BatchAtomsToTensor(atoms);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(3, 128));
    }

    [Fact]
    public void BatchAtomsToTensor_EmptyCollection_Throws()
    {
        // Act & Assert
        _sut.Invoking(s => s.BatchAtomsToTensor(Array.Empty<Atom>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BagOfAtoms_SimpleExpression_AggregatesCorrectly()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"), Atom.Sym("c"));

        // Act
        using var tensor = _sut.BagOfAtoms(expr, AggregationMode.Mean);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(128));
        // Should be normalized
        var span = tensor.AsSpan().ToArray();
        var norm = MathF.Sqrt(span.Sum(v => v * v));
        norm.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void BagOfAtoms_NestedExpression_FlattensAllSymbols()
    {
        // Arrange
        var nested = Atom.Expr(
            Atom.Expr(Atom.Sym("a"), Atom.Sym("b")),
            Atom.Sym("c"));

        // Act - Sum mode preserves magnitude
        using var tensor = _sut.BagOfAtoms(nested, AggregationMode.Sum);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(128));
        // With sum aggregation on 3 symbols, magnitude should be > 1
        var span = tensor.AsSpan().ToArray();
        var magnitude = MathF.Sqrt(span.Sum(v => v * v));
        magnitude.Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void ExpressionSignature_PreservesStructure()
    {
        // Arrange
        var expr1 = Atom.Expr(Atom.Sym("parent"), Atom.Sym("child"));
        var expr2 = Atom.Expr(Atom.Sym("parent"), Atom.Sym("child"));
        var expr3 = Atom.Expr(Atom.Sym("child"), Atom.Sym("parent"));

        // Act
        using var sig1 = _sut.ExpressionSignature(expr1);
        using var sig2 = _sut.ExpressionSignature(expr2);
        using var sig3 = _sut.ExpressionSignature(expr3);

        // Assert - same expression should have same signature
        sig1.AsSpan().ToArray().Should().Equal(sig2.AsSpan().ToArray());
        // Different order should produce different signature
        sig1.AsSpan().ToArray().Should().NotEqual(sig3.AsSpan().ToArray());
    }

    [Fact]
    public void ExpressionToTensorWeighted_AppliesWeightsCorrectly()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"));
        var weights = new float[] { 1.0f, 0.0f }; // Only first symbol

        // Act
        using var weighted = _sut.ExpressionToTensorWeighted(expr, weights);
        using var firstOnly = _sut.AtomToTensor(Atom.Sym("a"));

        // Assert - weighted with [1, 0] should equal just first symbol
        weighted.AsSpan().ToArray().Should().Equal(firstOnly.AsSpan().ToArray());
    }

    [Fact]
    public void GetStats_ReturnsCacheInformation()
    {
        // Arrange
        _sut.ClearCache();
        var _ = _sut.AtomToTensor(Atom.Sym("cached"));

        // Act
        var (cachedSymbols, dim) = _sut.GetStats();

        // Assert
        cachedSymbols.Should().Be(1); // "cached" symbol
        dim.Should().Be(128);
    }

    [Fact]
    public void ClearCache_ResetsCache()
    {
        // Arrange
        var _ = _sut.AtomToTensor(Atom.Sym("temp"));

        // Act
        _sut.ClearCache();
        var (count, _) = _sut.GetStats();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void AtomToVector_DifferentAtomTypes_AllWork()
    {
        // Arrange
        var symbol = Atom.Sym("s");
        var variable = Atom.Var("x");
        var expression = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"));

        // Act - all should not throw
        var vec1 = _sut.AtomToVector(symbol);
        var vec2 = _sut.AtomToVector(variable);
        var vec3 = _sut.AtomToVector(expression);

        // Assert
        vec1.Length.Should().Be(128);
        vec2.Length.Should().Be(128);
        vec3.Length.Should().Be(128);
    }

    // --- Semantic embedding path tests ---

    [Fact]
    public void WithoutProvider_UsesHashFallback()
    {
        // Arrange - adapter without embedding provider (default)
        var adapter = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128);
        var symbol = Atom.Sym("test-symbol");

        // Act
        var vec1 = adapter.AtomToVector(symbol);
        var vec2 = adapter.AtomToVector(symbol);

        // Assert - deterministic hash-based output
        vec1.Should().Equal(vec2);
        adapter.HasSemanticProvider.Should().BeFalse();
    }

    [Fact]
    public void WithProvider_SemanticPathPreferred()
    {
        // Arrange
        var expectedVec = MakeTestVector(128, 0.9f);
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["love"] = expectedVec
        });
        var adapter = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);
        var symbol = Atom.Sym("love");

        // Act
        var result = adapter.AtomToVector(symbol);

        // Assert - should use the provider's embedding, not hash
        result.Should().Equal(expectedVec);
    }

    [Fact]
    public void WithProvider_FallsBackWhenNotInProvider()
    {
        // Arrange - provider has no entry for "unknown-symbol"
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["love"] = MakeTestVector(128, 0.9f)
        });
        var adapterWithProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);
        var adapterWithoutProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false);
        var symbol = Atom.Sym("unknown-symbol");

        // Act
        var withProvider = adapterWithProvider.AtomToVector(symbol);
        var withoutProvider = adapterWithoutProvider.AtomToVector(symbol);

        // Assert - should fall back to hash (same as without provider)
        withProvider.Should().Equal(withoutProvider);
    }

    [Fact]
    public void WithProvider_FallsBackWhenDimensionMismatch()
    {
        // Arrange - provider returns wrong-length array
        var wrongDim = new float[64]; // 64 instead of 128
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["bad-dim"] = wrongDim
        });
        var adapterWithProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);
        var adapterWithoutProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false);
        var symbol = Atom.Sym("bad-dim");

        // Act
        var withProvider = adapterWithProvider.AtomToVector(symbol);
        var withoutProvider = adapterWithoutProvider.AtomToVector(symbol);

        // Assert - dimension mismatch should fall back to hash
        withProvider.Should().Equal(withoutProvider);
    }

    [Fact]
    public void SemanticAndHash_ProduceDifferentVectors()
    {
        // Arrange - same symbol, two adapters (one with provider, one without)
        var semanticVec = MakeTestVector(128, 0.9f);
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["love"] = semanticVec
        });
        var adapterWithProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);
        var adapterWithoutProvider = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false);
        var symbol = Atom.Sym("love");

        // Act
        var semantic = adapterWithProvider.AtomToVector(symbol);
        var hash = adapterWithoutProvider.AtomToVector(symbol);

        // Assert - semantic and hash vectors should differ
        semantic.Should().NotEqual(hash);
    }

    [Fact]
    public void HasSemanticProvider_ReflectsConfiguration()
    {
        // Arrange & Act
        var withProvider = new MeTTaToTensorAdapter(
            CpuTensorBackend.Instance, embeddingDim: 128,
            embeddingProvider: new TestEmbeddingProvider(new Dictionary<string, float[]>()));
        var withoutProvider = new MeTTaToTensorAdapter(
            CpuTensorBackend.Instance, embeddingDim: 128);

        // Assert
        withProvider.HasSemanticProvider.Should().BeTrue();
        withoutProvider.HasSemanticProvider.Should().BeFalse();
    }

    [Fact]
    public void VariableToVector_UsesSemanticPath()
    {
        // Arrange - provider has entry for "$X" (variable key format)
        var expectedVec = MakeTestVector(128, 1.5f);
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["$X"] = expectedVec
        });
        var adapter = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);
        var variable = Atom.Var("X");

        // Act
        var result = adapter.AtomToVector(variable);

        // Assert - variable should use semantic embedding
        result.Should().Equal(expectedVec);
    }

    [Fact]
    public void ExpressionToVector_RecursivelyUsesSemanticPath()
    {
        // Arrange - provider has entries for "a" and "b" but not "unknown"
        var vecA = MakeTestVector(128, 0.5f);
        var vecB = MakeTestVector(128, 0.7f);
        var provider = new TestEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["a"] = vecA,
            ["b"] = vecB
        });
        var adapter = new MeTTaToTensorAdapter(CpuTensorBackend.Instance, embeddingDim: 128, normalize: false, embeddingProvider: provider);

        // Build expression with "a" (semantic), "b" (semantic), "unknown" (hash fallback)
        var expression = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"), Atom.Sym("unknown"));

        // Act
        var result = adapter.AtomToVector(expression);

        // Assert - result should be a mean-pooled blend of semantic + hash vectors
        // "a" and "b" come from provider, "unknown" from hash
        result.Length.Should().Be(128);

        // The result should NOT be all zeros
        var sum = 0f;
        for (int i = 0; i < result.Length; i++)
            sum += MathF.Abs(result[i]);
        sum.Should().BeGreaterThan(0f);
    }
}