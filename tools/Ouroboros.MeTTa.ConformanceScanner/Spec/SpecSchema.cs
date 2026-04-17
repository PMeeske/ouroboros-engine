// <copyright file="SpecSchema.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Spec;

/// <summary>
/// All spec material grouped under a single operation name.
/// </summary>
public sealed record SpecSchema(
    string Name,
    IReadOnlyList<MeTtaSignature> Signatures,
    IReadOnlyList<MeTtaDefinition> Definitions,
    IReadOnlyList<string> RawForms);
