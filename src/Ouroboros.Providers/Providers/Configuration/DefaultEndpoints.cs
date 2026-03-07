// <copyright file="DefaultEndpoints.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Configuration;

/// <summary>
/// Default service endpoint constants used across the Ouroboros engine.
/// Centralizes localhost URLs to avoid hardcoded magic strings.
/// </summary>
public static class DefaultEndpoints
{
    /// <summary>
    /// Default Ollama local endpoint (HTTP API on port 11434).
    /// </summary>
    public const string Ollama = "http://localhost:11434";

    /// <summary>
    /// Default Qdrant REST endpoint (HTTP API on port 6333).
    /// </summary>
    public const string Qdrant = "http://localhost:6333";

    /// <summary>
    /// Default Qdrant gRPC endpoint (gRPC on port 6334).
    /// </summary>
    public const string QdrantGrpc = "http://localhost:6334";
}
