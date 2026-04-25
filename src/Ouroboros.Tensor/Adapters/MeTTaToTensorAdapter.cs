// <copyright file="MeTTaToTensorAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Ouroboros.Core.Hyperon;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Provides semantic embeddings for symbols when available.
/// When no provider is configured, the adapter falls back to deterministic hash-based vectors.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Attempts to retrieve a semantic embedding for the given symbol name.
    /// </summary>
    /// <param name="symbolName">The symbol to look up.</param>
    /// <param name="embeddingDim">Required embedding dimension.</param>
    /// <returns>A float array if a semantic embedding exists, or null if unavailable.</returns>
    float[]? TryGetEmbedding(string symbolName, int embeddingDim);
}

/// <summary>
/// Semantic bag-of-words adapter that converts MeTTa atoms and expressions
/// into tensor representations for neural operations.
///
/// <para>
/// This bridges the symbolic (Hyperon AtomSpace) and neural (ITensor) worlds:
/// <list type="bullet">
///   <item><description>Symbols are hashed to dense vectors</description></item>
///   <item><description>Expressions are aggregated via mean-pooling or attention</description></item>
///   <item><description>Atom spaces can be converted to batch tensors</description></item>
/// </list>
/// </para>
///
/// <para>
/// Use cases:
/// <list type="bullet">
///   <item><description>Neural-symbolic reasoning: convert atoms to tensors for similarity search</description></item>
///   <item><description>Grounding concepts: embed symbolic knowledge into vector space</description></item>
///   <item><description>Pattern matching acceleration: tensor-based similarity for fuzzy matching</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class MeTTaToTensorAdapter
{
    private readonly ITensorBackend _backend;
    private readonly int _embeddingDim;
    private readonly Dictionary<string, float[]> _symbolCache;
    private readonly bool _normalize;
    private readonly HashAlgorithm _hasher;
    private readonly IEmbeddingProvider? _embeddingProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaToTensorAdapter"/> class.
    /// Creates a new MeTTa-to-tensor adapter.
    /// </summary>
    /// <param name="backend">The tensor backend to use for creating tensors.</param>
    /// <param name="embeddingDim">Dimension of the output embeddings (default 256).</param>
    /// <param name="normalize">Whether to L2-normalize output vectors (default true).</param>
    /// <param name="embeddingProvider">Optional provider for semantic embeddings. When null, hash-based vectors are used.</param>
    public MeTTaToTensorAdapter(
        ITensorBackend backend,
        int embeddingDim = 256,
        bool normalize = true,
        IEmbeddingProvider? embeddingProvider = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        if (embeddingDim <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(embeddingDim), "Embedding dimension must be positive.");
        }

        _backend = backend;
        _embeddingDim = embeddingDim;
        _normalize = normalize;
        _embeddingProvider = embeddingProvider;
        _symbolCache = new Dictionary<string, float[]>(capacity: 1024);
        _hasher = SHA256.Create();
    }

    /// <summary>
    /// Gets a value indicating whether gets whether a semantic embedding provider is configured.
    /// When true, SymbolToVector and VariableToVector will attempt semantic lookup before falling back to hash.
    /// </summary>
    public bool HasSemanticProvider => _embeddingProvider != null;

    /// <summary>
    /// Converts a single atom to its tensor representation.
    /// </summary>
    /// <param name="atom">The atom to convert.</param>
    /// <returns>A tensor representing the atom's semantic content.</returns>
    public ITensor<float> AtomToTensor(Atom atom)
    {
        var vector = AtomToVector(atom);
        return _backend.FromMemory(vector.AsMemory(), TensorShape.Of(vector.Length));
    }

    /// <summary>
    /// Converts an atom to its vector representation (raw float array).
    /// Cached for symbols to improve performance.
    /// </summary>
    /// <param name="atom">The atom to convert.</param>
    /// <returns>A float array representing the atom's embedding.</returns>
    public float[] AtomToVector(Atom atom)
    {
        return atom switch
        {
            Symbol sym => SymbolToVector(sym),
            Variable var => VariableToVector(var),
            Expression expr => ExpressionToVector(expr),
            GroundedAtom grounded => GroundedToVector(grounded),
            _ => UnknownAtomToVector(atom),
        };
    }

    /// <summary>
    /// Converts an expression with weighted children using attention-style aggregation.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <param name="weights">Optional weights for each child (must match child count).</param>
    /// <returns>A tensor representing the weighted expression.</returns>
    public ITensor<float> ExpressionToTensorWeighted(Expression expression, float[]? weights = null)
    {
        var vector = ExpressionToVectorWeighted(expression, weights);
        return _backend.FromMemory(vector.AsMemory(), TensorShape.Of(vector.Length));
    }

    /// <summary>
    /// Converts multiple atoms to a batched tensor (2-D: [count, embeddingDim]).
    /// </summary>
    /// <param name="atoms">The atoms to batch.</param>
    /// <returns>A 2-D tensor with all atom embeddings.</returns>
    public ITensor<float> BatchAtomsToTensor(IEnumerable<Atom> atoms)
    {
        var atomList = atoms.ToList();
        if (atomList.Count == 0)
        {
            throw new ArgumentException("Cannot create batch from empty atom collection.", nameof(atoms));
        }

        var batch = new float[atomList.Count * _embeddingDim];

        for (int i = 0; i < atomList.Count; i++)
        {
            var vec = AtomToVector(atomList[i]);
            Array.Copy(vec, 0, batch, i * _embeddingDim, _embeddingDim);
        }

        return _backend.Create(TensorShape.Of(atomList.Count, _embeddingDim), batch);
    }

    /// <summary>
    /// Computes cosine similarity between two atoms via their tensor representations.
    /// </summary>
    /// <param name="a">First atom.</param>
    /// <param name="b">Second atom.</param>
    /// <returns>Cosine similarity in range [-1, 1].</returns>
    public float CosineSimilarity(Atom a, Atom b)
    {
        var vecA = AtomToVector(a);
        var vecB = AtomToVector(b);

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < _embeddingDim; i++)
        {
            dot += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }

        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom > 1e-8f ? dot / denom : 0f;
    }

    /// <summary>
    /// Creates a semantic "bag of atoms" tensor by summing/averaging all atoms in an expression tree.
    /// </summary>
    /// <param name="root">The root atom (typically an expression).</param>
    /// <param name="aggregation">How to combine atoms (Sum, Mean, Max).</param>
    /// <returns>A tensor representing the aggregated semantics.</returns>
    public ITensor<float> BagOfAtoms(Atom root, AggregationMode aggregation = AggregationMode.Mean)
    {
        var allSymbols = FlattenSymbols(root);
        if (allSymbols.Count == 0)
        {
            return _backend.Create(TensorShape.Of(_embeddingDim), new float[_embeddingDim]);
        }

        var result = new float[_embeddingDim];

        foreach (var sym in allSymbols)
        {
            var vec = SymbolToVector(sym);
            for (int i = 0; i < _embeddingDim; i++)
            {
                result[i] += vec[i];
            }
        }

        if (aggregation == AggregationMode.Mean)
        {
            var scale = 1.0f / allSymbols.Count;
            for (int i = 0; i < _embeddingDim; i++)
            {
                result[i] *= scale;
            }
        }
        else if (aggregation == AggregationMode.Max)
        {
            // For max pooling, we need to track absolute values
            var temp = new float[_embeddingDim];
            foreach (var sym in allSymbols)
            {
                var vec = SymbolToVector(sym);
                for (int i = 0; i < _embeddingDim; i++)
                {
                    if (MathF.Abs(vec[i]) > MathF.Abs(temp[i]))
                    {
                        temp[i] = vec[i];
                    }
                }
            }

            Array.Copy(temp, result, _embeddingDim);
        }

        if (_normalize)
        {
            Normalize(result);
        }

        return _backend.FromMemory(result, TensorShape.Of(_embeddingDim));
    }

    /// <summary>
    /// Computes the "signature" tensor of an expression, which captures structural patterns.
    /// Uses position-aware hashing to preserve some structure information.
    /// </summary>
    /// <param name="expression">The expression to sign.</param>
    /// <returns>A tensor signature of the expression.</returns>
    public ITensor<float> ExpressionSignature(Expression expression)
    {
        var result = new float[_embeddingDim];
        var position = 0;

        void Traverse(Atom atom, int depth)
        {
            if (atom is Symbol sym)
            {
                var vec = SymbolToVector(sym);

                // Weight by depth (deeper = less influence)
                var weight = MathF.Pow(0.8f, depth);
                var posWeight = 1.0f / (1.0f + position);
                position++;

                for (int i = 0; i < _embeddingDim; i++)
                {
                    result[i] += vec[i] * weight * posWeight;
                }
            }
            else if (atom is Expression expr)
            {
                for (int i = 0; i < expr.Children.Count; i++)
                {
                    Traverse(expr.Children[i], depth + 1);
                }
            }
        }

        Traverse(expression, 0);

        if (_normalize)
        {
            Normalize(result);
        }

        return _backend.FromMemory(result, TensorShape.Of(_embeddingDim));
    }

    // --- Private helpers ---
    private float[] SymbolToVector(Symbol sym)
    {
        if (_symbolCache.TryGetValue(sym.Name, out var cached))
        {
            return cached;
        }

        // Try semantic embedding provider first
        if (_embeddingProvider is not null)
        {
            var semantic = _embeddingProvider.TryGetEmbedding(sym.Name, _embeddingDim);
            if (semantic is not null && semantic.Length == _embeddingDim)
            {
                _symbolCache[sym.Name] = semantic;
                return semantic;
            }
        }

        // Fall back to deterministic hash-based vector
        var vec = HashToVector(sym.Name);
        _symbolCache[sym.Name] = vec;
        return vec;
    }

    private float[] VariableToVector(Variable var)
    {
        // Variables get special treatment - they represent "unknowns"
        // Use a consistent embedding based on variable name
        var key = "$" + var.Name;
        if (_symbolCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Try semantic embedding provider first
        if (_embeddingProvider is not null)
        {
            var semantic = _embeddingProvider.TryGetEmbedding(key, _embeddingDim);
            if (semantic is not null && semantic.Length == _embeddingDim)
            {
                _symbolCache[key] = semantic;
                return semantic;
            }
        }

        // Fall back to deterministic hash-based vector
        var vec = HashToVector(key, offset: 0.5f); // Offset to distinguish from symbols
        _symbolCache[key] = vec;
        return vec;
    }

    private float[] ExpressionToVector(Expression expr)
    {
        // Mean-pool over children
        var result = new float[_embeddingDim];

        if (expr.Children.Count == 0)
        {
            return result;
        }

        foreach (var child in expr.Children)
        {
            var childVec = AtomToVector(child);
            for (int i = 0; i < _embeddingDim; i++)
            {
                result[i] += childVec[i];
            }
        }

        var scale = 1.0f / expr.Children.Count;
        for (int i = 0; i < _embeddingDim; i++)
        {
            result[i] *= scale;
        }

        if (_normalize)
        {
            Normalize(result);
        }

        return result;
    }

    private float[] ExpressionToVectorWeighted(Expression expr, float[]? weights)
    {
        var result = new float[_embeddingDim];

        if (expr.Children.Count == 0)
        {
            return result;
        }

        var useWeights = weights != null && weights.Length == expr.Children.Count;
        var totalWeight = 0f;

        for (int i = 0; i < expr.Children.Count; i++)
        {
            var childVec = AtomToVector(expr.Children[i]);
            var w = useWeights ? weights![i] : 1.0f;
            totalWeight += w;

            for (int j = 0; j < _embeddingDim; j++)
            {
                result[j] += childVec[j] * w;
            }
        }

        if (totalWeight > 0)
        {
            var scale = 1.0f / totalWeight;
            for (int i = 0; i < _embeddingDim; i++)
            {
                result[i] *= scale;
            }
        }

        if (_normalize)
        {
            Normalize(result);
        }

        return result;
    }

    private float[] GroundedToVector(GroundedAtom grounded)
    {
        // Grounded atoms can have custom embeddings based on their grounded value
        var result = new float[_embeddingDim];

        // Use the grounded operation name as base
        var opName = grounded.OperationName ?? "grounded";
        var baseVec = HashToVector(opName);

        Array.Copy(baseVec, result, _embeddingDim);

        // If the grounded value is a number, encode it specially
        if (grounded.Value is double d)
        {
            // Encode numeric value in first few dimensions
            result[0] = (float)Math.Tanh(d / 10.0); // Normalized
            result[1] = (float)(d > 0 ? 1 : d < 0 ? -1 : 0); // Sign
        }
        else if (grounded.Value is string s)
        {
            // String values get hashed
            var strVec = HashToVector(s, offset: 0.25f);
            for (int i = 0; i < _embeddingDim; i++)
            {
                result[i] = (result[i] + strVec[i]) * 0.5f;
            }
        }

        if (_normalize)
        {
            Normalize(result);
        }

        return result;
    }

    private float[] UnknownAtomToVector(Atom atom)
    {
        // Fallback for unknown atom types
        return HashToVector($"unknown:{atom.ToSExpr()}");
    }

    private float[] HashToVector(string input, float offset = 0f)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = _hasher.ComputeHash(bytes);

        var result = new float[_embeddingDim];

        // Use hash bytes to initialize the vector
        // SHA256 gives us 32 bytes = 256 bits, perfect for embedding
        for (int i = 0; i < _embeddingDim && i < hash.Length; i++)
        {
            // Convert byte to float in [-1, 1] range
            result[i] = (hash[i % hash.Length] / 128.0f) - 1.0f + offset;
        }

        // For dimensions > 256, use linear combinations of hash bytes
        for (int i = hash.Length; i < _embeddingDim; i++)
        {
            var h1 = hash[i % hash.Length];
            var h2 = hash[(i + 7) % hash.Length];
            var h3 = hash[(i + 13) % hash.Length];
            result[i] = ((h1 ^ h2) / 128.0f) - 1.0f + offset;
        }

        if (_normalize)
        {
            Normalize(result);
        }

        return result;
    }

    private static ImmutableList<Symbol> FlattenSymbols(Atom atom)
    {
        var result = ImmutableList.CreateBuilder<Symbol>();
        FlattenSymbolsInto(atom, result);
        return result.ToImmutable();
    }

    private static void FlattenSymbolsInto(Atom atom, ImmutableList<Symbol>.Builder result)
    {
        switch (atom)
        {
            case Symbol sym:
                result.Add(sym);
                break;
            case Expression expr:
                foreach (var child in expr.Children)
                {
                    FlattenSymbolsInto(child, result);
                }

                break;
        }
    }

    private static void Normalize(Span<float> vector)
    {
        float norm = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }

        if (norm > 0)
        {
            var scale = 1.0f / MathF.Sqrt(norm);
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] *= scale;
            }
        }
    }

    /// <summary>
    /// Clears the internal symbol cache.
    /// </summary>
    public void ClearCache() => _symbolCache.Clear();

    /// <summary>
    /// Gets statistics about the adapter's cache.
    /// </summary>
    /// <returns></returns>
    public (int CachedSymbols, int EmbeddingDim) GetStats()
        => (_symbolCache.Count, _embeddingDim);
}

/// <summary>
/// How to aggregate multiple atom vectors into a single representation.
/// </summary>
public enum AggregationMode
{
    /// <summary>Sum all vectors (preserves magnitude).</summary>
    Sum,

    /// <summary>Mean of all vectors (normalizes by count).</summary>
    Mean,

    /// <summary>Max pooling (preserves strongest features).</summary>
    Max,
}

/// <summary>
/// Base class for grounded atoms that carry executable operations.
/// </summary>
public abstract record GroundedAtom : Atom
{
    /// <summary>
    /// Gets the name of the grounded operation.
    /// </summary>
    public abstract string OperationName { get; }

    /// <summary>
    /// Gets optional value associated with this grounded atom.
    /// </summary>
    public virtual object? Value => null;

    /// <inheritdoc/>
    public override string ToSExpr() => $"({OperationName} ...)";
}
