// <copyright file="MeTtaDefinition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Spec;

/// <summary>
/// Implementation form: (= (name args...) body).
/// </summary>
/// <param name="Head">The operation name extracted from the head expression.</param>
/// <param name="RawBody">The body as serialized MeTTa source text.</param>
/// <param name="ArgArity">Count of arguments in the head (excludes the name symbol itself).</param>
public sealed record MeTtaDefinition(string Head, string RawBody, int ArgArity);
