// <copyright file="FileLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Vectors;

namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Loads a single file into a document.
/// Replaces LangChain.DocumentLoaders.FileLoader.
/// </summary>
public class FileLoader : IDocumentLoader
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        if (source.Value is not string path || !System.IO.File.Exists(path))
        {
            return Array.Empty<Document>();
        }

        string content;
        try
        {
            content = await System.IO.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return Array.Empty<Document>();
        }

        return new[]
        {
            new Document(content, new Dictionary<string, object>
            {
                ["path"] = path,
                ["name"] = System.IO.Path.GetFileName(path)
            })
        };
    }
}
