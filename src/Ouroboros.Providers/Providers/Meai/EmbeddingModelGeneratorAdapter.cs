// <copyright file="EmbeddingModelGeneratorAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Wraps an Ouroboros <see cref="IEmbeddingModel"/> as a MEAI
/// <see cref="IEmbeddingGenerator{String, Embedding}"/>.
/// </summary>
public sealed class EmbeddingModelGeneratorAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IEmbeddingModel _model;

    public EmbeddingModelGeneratorAdapter(IEmbeddingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    private static readonly EmbeddingGeneratorMetadata s_metadata = new(nameof(EmbeddingModelGeneratorAdapter));

    /// <inheritdoc/>
    public EmbeddingGeneratorMetadata Metadata => s_metadata;

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Embedding<float>>();
        foreach (string input in values)
        {
            float[] vector = await _model.CreateEmbeddingsAsync(input, cancellationToken)
                .ConfigureAwait(false);
            results.Add(new Embedding<float>(vector));
        }

        return new GeneratedEmbeddings<Embedding<float>>(results);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null)
    {
        if (key is not null) return null;

        if (serviceType == typeof(IEmbeddingModel))
            return _model;

        if (serviceType?.IsAssignableFrom(GetType()) == true)
            return this;

        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged resources owned.
    }
}
