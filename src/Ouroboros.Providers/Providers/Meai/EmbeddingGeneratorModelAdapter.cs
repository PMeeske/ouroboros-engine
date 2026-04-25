// <copyright file="EmbeddingGeneratorModelAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Wraps a MEAI <see cref="IEmbeddingGenerator{String, Embedding}"/>
/// as an Ouroboros <see cref="IEmbeddingModel"/>.
/// This is the reverse of <see cref="EmbeddingModelGeneratorAdapter"/>.
/// </summary>
public sealed class EmbeddingGeneratorModelAdapter : IEmbeddingModel
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public EmbeddingGeneratorModelAdapter(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        GeneratedEmbeddings<Embedding<float>> result = await _generator
            .GenerateAsync([input], cancellationToken: ct)
            .ConfigureAwait(false);

        if (result.Count == 0)
        {
            return [];
        }

        return result[0].Vector.ToArray();
    }
}
