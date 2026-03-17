// <copyright file="ExpressionPatternRecord.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.VectorData;

namespace Ouroboros.SemanticKernel.VectorData;

/// <summary>
/// SK-compatible record model for NanoAtom grammar evolution expression patterns.
/// Maps expression pattern data to/from the SK <see cref="VectorStoreCollection{TKey, TRecord}"/>
/// abstraction backed by the Qdrant <c>ouroboros_expression_patterns</c> collection.
/// </summary>
internal sealed class ExpressionPatternRecord
{
    /// <summary>
    /// Gets or sets the unique pattern identifier.
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expression pattern text (e.g. ANTLR grammar fragment).
    /// </summary>
    [VectorStoreData]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern category (e.g. "rule", "token", "fragment").
    /// </summary>
    [VectorStoreData]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fitness score from grammar evolution (0-1).
    /// </summary>
    [VectorStoreData]
    public double Fitness { get; set; }

    /// <summary>
    /// Gets or sets the generation number from the evolutionary process.
    /// </summary>
    [VectorStoreData]
    public int Generation { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector.
    /// </summary>
    // Note: Runtime dimension from BuildDefinition() takes precedence over this attribute value
    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    /// <summary>
    /// Builds a <see cref="VectorStoreCollectionDefinition"/> for this record type
    /// with the specified vector dimension.
    /// </summary>
    internal static VectorStoreCollectionDefinition BuildDefinition(int vectorDimension)
    {
        return new VectorStoreCollectionDefinition
        {
            Properties = new List<VectorStoreProperty>
            {
                new VectorStoreKeyProperty(nameof(Id), typeof(string)),
                new VectorStoreDataProperty(nameof(Pattern), typeof(string)),
                new VectorStoreDataProperty(nameof(Category), typeof(string)),
                new VectorStoreDataProperty(nameof(Fitness), typeof(double)),
                new VectorStoreDataProperty(nameof(Generation), typeof(int)),
                new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>), vectorDimension),
            },
        };
    }
}
