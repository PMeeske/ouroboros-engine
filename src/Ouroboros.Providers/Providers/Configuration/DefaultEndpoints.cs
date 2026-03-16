// <copyright file="DefaultEndpoints.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using CoreDefaults = Ouroboros.Core.Configuration.DefaultEndpoints;

namespace Ouroboros.Providers.Configuration;

/// <summary>
/// Default service endpoint constants used across the Ouroboros engine.
/// Delegates to the canonical source in <see cref="CoreDefaults"/>.
/// Kept for backward compatibility with existing <c>using Ouroboros.Providers.Configuration;</c> imports.
/// </summary>
public static class DefaultEndpoints
{
    /// <summary>
    /// Default Ollama local endpoint (HTTP API on port 11434).
    /// </summary>
    public const string Ollama = CoreDefaults.Ollama;

    /// <summary>
    /// Default Qdrant REST endpoint (HTTP API on port 6333).
    /// </summary>
    public const string Qdrant = CoreDefaults.Qdrant;

    /// <summary>
    /// Default Qdrant gRPC endpoint (gRPC on port 6334).
    /// </summary>
    public const string QdrantGrpc = CoreDefaults.QdrantGrpc;
}
