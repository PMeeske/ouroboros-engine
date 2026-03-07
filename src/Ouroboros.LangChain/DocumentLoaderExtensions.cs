// <copyright file="DocumentLoaderExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.DocumentLoaders;
using LangChain.Providers;

namespace Ouroboros.LangChainBridge;

/// <summary>
/// Helper extensions bridging LangChain <see cref="Document"/> with Ouroboros pipeline concepts.
/// </summary>
public static class DocumentLoaderExtensions
{
    /// <summary>
    /// Converts a collection of <see cref="Document"/> to a single LangChain
    /// <see cref="Message"/> suitable for use as context in a chat request.
    /// </summary>
    /// <param name="documents">The documents to concatenate.</param>
    /// <param name="separator">Separator between document contents.</param>
    /// <returns>A system message containing the concatenated document content.</returns>
    public static Message ToContextMessage(
        this IEnumerable<Document> documents,
        string separator = "\n\n---\n\n")
    {
        ArgumentNullException.ThrowIfNull(documents);

        string content = string.Join(separator,
            documents.Select(d => d.PageContent));

        return new Message(content, MessageRole.System);
    }

    /// <summary>
    /// Creates a <see cref="ChatRequest"/> with document context prepended as a system message
    /// followed by the user query.
    /// </summary>
    /// <param name="documents">Context documents.</param>
    /// <param name="query">The user's query.</param>
    /// <param name="separator">Separator between document contents.</param>
    /// <returns>A chat request with context + query.</returns>
    public static ChatRequest ToRagRequest(
        this IEnumerable<Document> documents,
        string query,
        string separator = "\n\n---\n\n")
    {
        ArgumentNullException.ThrowIfNull(documents);

        var contextMessage = documents.ToContextMessage(separator);
        var queryMessage = new Message(query, MessageRole.Human);

        return new ChatRequest
        {
            Messages = new List<Message> { contextMessage, queryMessage }
        };
    }
}
